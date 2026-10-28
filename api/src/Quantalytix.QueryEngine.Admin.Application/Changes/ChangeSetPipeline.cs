using System.Globalization;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>
/// The change-set protocol (blueprint 01, OI-01) for one aggregate, generalizing <c>ReportChanges</c> and <c>ModelChanges</c>:
/// load the aggregate as stored, then for each operation in order validate its input and apply it in memory, then run the gates
/// on the result. A preview stops there and reports the findings of the publication and commit rules. A commit runs the commit
/// rules, then plans the row writes, makes them in <see cref="WritePlan"/> order, advances the semantic revision once when the
/// engine reads the change, and answers the aggregate as stored with the real ids of the temporary ones. Any refusal writes
/// nothing. Findings name the items the change set created by their temporary ids, like the preview's state.
/// </summary>
/// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
/// <typeparam name="TOperation">The aggregate's operation union.</typeparam>
/// <typeparam name="TState">The content read model.</typeparam>
/// <param name="aggregate">The aggregate's load, apply, plan and read.</param>
/// <param name="rules">The aggregate's snapshot rules, in registration order.</param>
/// <param name="validators">The operation input validators; an operation with none passes.</param>
/// <param name="ids">Allocates the real ids of temporary ones.</param>
/// <param name="semanticRevision">The tenant's semantic revision.</param>
public sealed class ChangeSetPipeline<TSnapshot, TOperation, TState>(
    IChangeSetAggregate<TSnapshot, TOperation, TState> aggregate,
    IEnumerable<ISnapshotRule<TSnapshot>> rules,
    IEnumerable<IValidator<TOperation>> validators,
    IIdGenerator ids,
    SemanticRevision semanticRevision)
    where TOperation : notnull
    where TState : notnull
{
    /// <summary>The detail of the 422 refusal of a gate or commit rule.</summary>
    private const string RulesViolatedDetail = "The change would break rules; nothing was written.";

    /// <summary>Applies the operations in memory and writes nothing.</summary>
    /// <param name="aggregateId">The aggregate root's id.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending state with the publication and commit findings, or the refusal of the first operation or gate that fails.</returns>
    public async Task<Outcome<ChangeSetPreview<TState>>> PreviewAsync(
        long aggregateId,
        ChangeSetRequest<TOperation> request,
        CancellationToken cancellationToken)
    {
        var applied = await ApplyAsync(aggregateId, request, cancellationToken);
        if (applied.Refusal is { } refusal) return Outcome<ChangeSetPreview<TState>>.Refused(refusal);
        return Outcome<ChangeSetPreview<TState>>.Success(new ChangeSetPreview<TState>
        {
            State = await aggregate.ReadAsync(applied.After, applied.Ids.Present, cancellationToken),
            Violations = await EvaluateAsync([RuleStage.Publication, RuleStage.Commit], applied.After, applied.Ids, cancellationToken)
        });
    }

    /// <summary>Applies the operations to the aggregate as stored now and writes the result.</summary>
    /// <param name="aggregateId">The aggregate root's id.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored state and id map, or the refusal of the first operation, gate or commit rule that fails, with nothing written.</returns>
    public async Task<Outcome<ChangeSetCommit<TState>>> CommitAsync(
        long aggregateId,
        ChangeSetRequest<TOperation> request,
        CancellationToken cancellationToken)
    {
        var applied = await ApplyAsync(aggregateId, request, cancellationToken);
        if (applied.Refusal is { } refusal) return Outcome<ChangeSetCommit<TState>>.Refused(refusal);
        var findings = await EvaluateAsync([RuleStage.Commit], applied.After, applied.Ids, cancellationToken);
        if (findings.Count > 0) return Outcome<ChangeSetCommit<TState>>.Refused(Refusal.RulesViolated(RulesViolatedDetail, findings));
        var plan = new WritePlan();
        aggregate.Plan(applied.Before, applied.After, plan);
        foreach (var write in plan.Ordered()) await write.ExecuteAsync(cancellationToken);
        if (aggregate.IsEngineVisible(applied.Before, applied.After)) await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<ChangeSetCommit<TState>>.Success(new ChangeSetCommit<TState>
        {
            State = await aggregate.ReadAsync(await aggregate.LoadAsync(aggregateId, cancellationToken), TemporaryIds.PresentStored, cancellationToken),
            IdMap = applied.Ids.Map
        });
    }

    /// <summary>Loads the aggregate, validates and applies each operation, and runs the gates; stops at the first refusal.</summary>
    private async Task<Applied> ApplyAsync(long aggregateId, ChangeSetRequest<TOperation> request, CancellationToken cancellationToken)
    {
        var temporaryIds = new TemporaryIds(ids);
        var context = new ChangeContext(temporaryIds);
        var before = await aggregate.LoadAsync(aggregateId, cancellationToken);
        var after = before;
        for (var index = 0; index < request.Operations.Count; index++)
        {
            context.Begin(index);
            var operation = request.Operations[index];
            if (await InputRefusalAsync(operation, context.OperationPath, cancellationToken) is { } invalid) return new Applied(before, after, temporaryIds, invalid);
            after = aggregate.Apply(after, operation, context);
            if (context.Raised() is { } refused) return new Applied(before, after, temporaryIds, refused);
        }

        var gates = await EvaluateAsync([RuleStage.Gate], after, temporaryIds, cancellationToken);
        var refusal = gates.Count > 0 ? Refusal.RulesViolated(RulesViolatedDetail, gates) : null;
        return new Applied(before, after, temporaryIds, refusal);
    }

    /// <summary>The 400 refusal of an operation whose input fails a validator, keyed under the operation's path; null when it passes.</summary>
    private async Task<Refusal?> InputRefusalAsync(TOperation operation, string operationPath, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(operation, cancellationToken);
            foreach (var failure in result.Errors)
            {
                var key = failure.PropertyName.Length == 0 ? operationPath : $"{operationPath}.{failure.PropertyName}";
                if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count == 0) return null;
        return Refusal.Invalid(errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal));
    }

    /// <summary>The violations of every rule of the stages, in registration order, each target item presented as the client names it.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(RuleStage[] stages, TSnapshot snapshot, TemporaryIds temporaryIds, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var rule in rules.Where(rule => stages.Contains(rule.Stage))) violations.AddRange(await rule.EvaluateAsync(snapshot, cancellationToken));
        return [.. violations.Select(violation => Presented(violation, temporaryIds))];
    }

    /// <summary>A violation whose target item, which a rule names by its digits, is written by the temporary id it was allocated for.</summary>
    private static RuleViolation Presented(RuleViolation violation, TemporaryIds temporaryIds)
    {
        if (violation.Target is not { ItemId: { } itemId } target) return violation;
        return violation with { Target = target with { ItemId = temporaryIds.Present(long.Parse(itemId, NumberStyles.None, CultureInfo.InvariantCulture)) } };
    }

    /// <summary>The aggregate before and after the operations, the change set's ids, and the refusal that stopped it, if any.</summary>
    private sealed record Applied(TSnapshot Before, TSnapshot After, TemporaryIds Ids, Refusal? Refusal);
}

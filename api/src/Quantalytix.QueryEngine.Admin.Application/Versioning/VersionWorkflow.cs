using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Moves versions through their lifecycle: checks the transition, derives data and runs the publication rules when a
/// version leaves draft, runs the retirement rules when a published version is retired, writes the status, cascades a
/// publish and advances the semantic revision when published content reaches queries directly.
/// </summary>
public sealed class VersionWorkflow(
    VersionStoreRegistry stores,
    IEnumerable<IPublicationRule> publicationRules,
    IEnumerable<IRetirementRule> retirementRules,
    IEnumerable<IVersionLifecycleHandler> handlers,
    SemanticRevision semanticRevision,
    TimeProvider time)
{
    /// <summary>
    /// The kinds whose published content queries resolve without a release activation, so publishing or retiring one must
    /// invalidate cached metadata. Dataset, calculation model and report versions, and releases, reach queries only through
    /// the activated release that pins them.
    /// </summary>
    private static readonly FrozenSet<string> RevisionKinds = FrozenSet.Create(
        StringComparer.Ordinal,
        VersionKinds.DimensionVersion,
        VersionKinds.MappingVersion,
        VersionKinds.BridgeVersion,
        VersionKinds.AssumptionSet);

    /// <summary>Requests a status transition.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="targetStatus">The status to move to.</param>
    /// <param name="cancellationToken">Cancels the transition between its steps.</param>
    /// <returns>Applied with the new state, NotAllowed with the current state, or Rejected with the rule violations.</returns>
    public async Task<TransitionResult> TransitionAsync(string kind, long id, string targetStatus, CancellationToken cancellationToken)
    {
        var store = stores.Get(kind);
        var current = await store.GetAsync(id, cancellationToken);
        if (!VersionTransitions.IsAllowed(kind, current.Status, targetStatus))
            return new TransitionResult { Outcome = TransitionOutcome.NotAllowed, State = current, Violations = [] };

        if (VersionTransitions.RunsPublicationRules(kind, current.Status, targetStatus))
        {
            foreach (var handler in HandlersOf(kind)) await handler.OnValidatingAsync(id, cancellationToken);
            var violations = await EvaluateRulesAsync(publicationRules, kind, id, cancellationToken);
            if (violations.Count > 0)
                return new TransitionResult { Outcome = TransitionOutcome.Rejected, State = current, Violations = violations };
        }

        if (VersionTransitions.RunsRetirementRules(current.Status, targetStatus))
        {
            var violations = await EvaluateRulesAsync(retirementRules, kind, id, cancellationToken);
            if (violations.Count > 0)
                return new TransitionResult { Outcome = TransitionOutcome.Rejected, State = current, Violations = violations };
        }

        var publishing = targetStatus == PublicationStatuses.Published;
        var publishedAt = current.PublishedAt;
        if (publishing) publishedAt = time.GetUtcNow().UtcDateTime;
        await store.SetStatusAsync(id, targetStatus, publishedAt, cancellationToken);
        if (publishing)
            foreach (var handler in HandlersOf(kind)) await handler.OnPublishedAsync(id, cancellationToken);
        if (RevisionKinds.Contains(kind) && (publishing || targetStatus == PublicationStatuses.Retired))
            await semanticRevision.AdvanceAsync(cancellationToken);

        return new TransitionResult
        {
            Outcome = TransitionOutcome.Applied,
            State = current with { Status = targetStatus, PublishedAt = publishedAt },
            Violations = []
        };
    }

    /// <summary>The lifecycle handlers of a kind, in registration order.</summary>
    private IEnumerable<IVersionLifecycleHandler> HandlersOf(string kind)
    {
        return handlers.Where(handler => handler.Kind == kind);
    }

    /// <summary>Runs every rule of a kind, one after another in registration order, and collects their violations.</summary>
    private static async Task<IReadOnlyList<RuleViolation>> EvaluateRulesAsync(
        IEnumerable<IVersionRule> rules,
        string kind,
        long id,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var rule in rules.Where(rule => rule.Kind == kind)) violations.AddRange(await rule.EvaluateAsync(id, cancellationToken));
        return violations;
    }
}

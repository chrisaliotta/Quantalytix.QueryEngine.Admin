using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Moves versions through their lifecycle: checks the transition, derives data and runs the publication rules when a version
/// leaves draft, runs the retirement rules when a published version is retired, writes the status, and runs the kind's publish
/// handlers. A living kind's publish reaches queries without a release activation, so it also advances the semantic revision.
/// Checks a version against its publication rules without changing its status, too.
/// </summary>
public sealed class VersionWorkflow(
    VersionStoreRegistry stores,
    IEnumerable<IPublicationRule> publicationRules,
    IEnumerable<IRetirementRule> retirementRules,
    IEnumerable<IVersionLifecycleHandler> handlers,
    SemanticRevision semanticRevision,
    TimeProvider time)
{
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

        if (VersionTransitions.RunsPublicationRules(current.Status, targetStatus))
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
        if (publishing && VersionKinds.Living.Contains(kind)) await semanticRevision.AdvanceAsync(cancellationToken);

        return new TransitionResult
        {
            Outcome = TransitionOutcome.Applied,
            State = current with { Status = targetStatus, PublishedAt = publishedAt },
            Violations = []
        };
    }

    /// <summary>
    /// Runs a version's publication rules and changes nothing: the Validate check of a published living version, whose edits
    /// only field rules guard as they are made.
    /// </summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the check between rules.</param>
    /// <returns>The violations; empty when the version passes.</returns>
    public Task<IReadOnlyList<RuleViolation>> CheckAsync(string kind, long id, CancellationToken cancellationToken)
    {
        return EvaluateRulesAsync(publicationRules, kind, id, cancellationToken);
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

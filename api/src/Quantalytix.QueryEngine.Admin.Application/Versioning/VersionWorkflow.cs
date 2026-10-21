using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Moves versions through their lifecycle: checks the transition, derives data and runs the publication rules when a version leaves
/// draft, runs the retirement rules when a published version is retired, writes the status, and runs the kind's publish handlers.
/// While the rules run, a version of a living kind counts as its identity's published version, so it checks against itself. A
/// living kind's publish reaches queries without a release activation, so it also advances the semantic revision. Checks a version
/// against its publication rules without changing its status, too.
/// </summary>
public sealed class VersionWorkflow(
    VersionStoreRegistry stores,
    IEnumerable<IPublicationRule> publicationRules,
    IEnumerable<IRetirementRule> retirementRules,
    IEnumerable<IVersionLifecycleHandler> handlers,
    PublicationSet publicationSet,
    SemanticRevision semanticRevision,
    TimeProvider time)
{
    /// <summary>Requests a status transition of one version.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="targetStatus">The status to move to.</param>
    /// <param name="cancellationToken">Cancels the transition between its steps.</param>
    /// <returns>Applied with the new state, NotAllowed with the current state, or Rejected with the rule violations.</returns>
    public async Task<TransitionResult> TransitionAsync(string kind, long id, string targetStatus, CancellationToken cancellationToken)
    {
        var current = await stores.Get(kind).GetAsync(id, cancellationToken);
        if (!VersionTransitions.IsAllowed(kind, current.Status, targetStatus))
            return new TransitionResult { Outcome = TransitionOutcome.NotAllowed, State = current, Violations = [] };

        var violations = await TransitionRulesAsync(current, targetStatus, cancellationToken);
        if (violations.Count > 0)
            return new TransitionResult { Outcome = TransitionOutcome.Rejected, State = current, Violations = violations };

        var publishing = targetStatus == PublicationStatuses.Published;
        var publishedAt = publishing ? time.GetUtcNow().UtcDateTime : current.PublishedAt;
        await stores.Get(kind).SetStatusAsync(id, targetStatus, publishedAt, cancellationToken);
        if (publishing)
        {
            foreach (var handler in HandlersOf(kind)) await handler.OnPublishedAsync(id, cancellationToken);
            if (VersionKinds.Living.Contains(kind)) await semanticRevision.AdvanceAsync(cancellationToken);
        }

        return new TransitionResult
        {
            Outcome = TransitionOutcome.Applied,
            State = current with { Status = targetStatus, PublishedAt = publishedAt },
            Violations = []
        };
    }

    /// <summary>
    /// Publishes a new draft through both of its transitions: the report editor's way with the report dimension and bridges it
    /// creates, each with one version that is published from the start and edited in place.
    /// </summary>
    /// <param name="kind">A living <c>version_kind</c> value.</param>
    /// <param name="id">The draft's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the transitions between their steps.</param>
    /// <returns>A task that completes when the version is published.</returns>
    public async Task PublishAsync(string kind, long id, CancellationToken cancellationToken)
    {
        await TransitionAsync(kind, id, PublicationStatuses.Validated, cancellationToken);
        await TransitionAsync(kind, id, PublicationStatuses.Published, cancellationToken);
    }

    /// <summary>
    /// Runs a version's publication rules and changes nothing: the Validate check of a published living version, whose edits
    /// only field rules guard as they are made.
    /// </summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="id">The version's surrogate key.</param>
    /// <param name="cancellationToken">Cancels the check between rules.</param>
    /// <returns>The violations; empty when the version passes.</returns>
    public async Task<IReadOnlyList<RuleViolation>> CheckAsync(string kind, long id, CancellationToken cancellationToken)
    {
        return await EvaluateRulesAsync(publicationRules, await stores.Get(kind).GetAsync(id, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// The violations of the rules a transition runs: leaving draft, the validating handlers derive data and the publication rules
    /// run; retiring a published version, the retirement rules run; any other transition runs none.
    /// </summary>
    private async Task<IReadOnlyList<RuleViolation>> TransitionRulesAsync(VersionState current, string targetStatus, CancellationToken cancellationToken)
    {
        if (VersionTransitions.RunsPublicationRules(current.Status, targetStatus))
        {
            foreach (var handler in HandlersOf(current.Kind)) await handler.OnValidatingAsync(current.Id, cancellationToken);
            return await EvaluateRulesAsync(publicationRules, current, cancellationToken);
        }

        return VersionTransitions.RunsRetirementRules(current.Status, targetStatus)
            ? await EvaluateRulesAsync(retirementRules, current, cancellationToken)
            : [];
    }

    /// <summary>The lifecycle handlers of a kind, in registration order.</summary>
    private IEnumerable<IVersionLifecycleHandler> HandlersOf(string kind)
    {
        return handlers.Where(handler => handler.Kind == kind);
    }

    /// <summary>
    /// Runs every rule of the version's kind in registration order, with a version of a living kind counted as its identity's
    /// published version, and collects the violations.
    /// </summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateRulesAsync(
        IEnumerable<IVersionRule> rules,
        VersionState version,
        CancellationToken cancellationToken)
    {
        if (VersionKinds.Living.Contains(version.Kind))
            publicationSet.Add(version.Kind, await stores.GetLiving(version.Kind).IdentityIdAsync(version.Id, cancellationToken), version.Id);
        var violations = new List<RuleViolation>();
        foreach (var rule in rules.Where(rule => rule.Kind == version.Kind)) violations.AddRange(await rule.EvaluateAsync(version.Id, cancellationToken));
        publicationSet.Clear();
        return violations;
    }
}

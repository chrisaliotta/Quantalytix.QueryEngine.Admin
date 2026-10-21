using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Moves versions through their lifecycle, one at a time or as a set that moves together: checks every transition, derives data
/// and runs the publication rules when the versions leave draft, runs the retirement rules when published versions are retired,
/// writes the statuses, and runs the kinds' publish handlers. While the rules of a set run, each version of a living kind counts as
/// its identity's published version, so the set validates as a whole or not at all. A living kind's publish reaches queries
/// without a release activation, so it also advances the semantic revision, once per transition. Checks versions against their
/// publication rules without changing their status, too.
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
    public Task<TransitionResult> TransitionAsync(string kind, long id, string targetStatus, CancellationToken cancellationToken)
    {
        return TransitionAsync([new VersionReference { Kind = kind, Id = id }], targetStatus, cancellationToken);
    }

    /// <summary>
    /// Requests the same status transition of a set of versions that moves together. Every version's transition must be allowed,
    /// and leaving draft every version must pass its rules, before any status changes; statuses are written, and publish handlers
    /// run, in the order given.
    /// </summary>
    /// <param name="versions">The versions, in the order their statuses are written.</param>
    /// <param name="targetStatus">The status to move to.</param>
    /// <param name="cancellationToken">Cancels the transition between its steps.</param>
    /// <returns>Applied with the new states, NotAllowed with the current states, or Rejected with every version's rule violations.</returns>
    public async Task<TransitionResult> TransitionAsync(IReadOnlyList<VersionReference> versions, string targetStatus, CancellationToken cancellationToken)
    {
        var current = await StatesAsync(versions, cancellationToken);
        if (current.Any(state => !VersionTransitions.IsAllowed(state.Kind, state.Status, targetStatus)))
            return new TransitionResult { Outcome = TransitionOutcome.NotAllowed, States = current, Violations = [] };

        var validating = current.Where(state => VersionTransitions.RunsPublicationRules(state.Status, targetStatus)).ToList();
        foreach (var state in validating)
            foreach (var handler in HandlersOf(state.Kind)) await handler.OnValidatingAsync(state.Id, cancellationToken);
        var violations = await EvaluateRulesAsync(publicationRules, validating, cancellationToken);
        violations = [.. violations, .. await EvaluateRulesAsync(
            retirementRules, [.. current.Where(state => VersionTransitions.RunsRetirementRules(state.Status, targetStatus))], cancellationToken)];
        if (violations.Count > 0)
            return new TransitionResult { Outcome = TransitionOutcome.Rejected, States = current, Violations = violations };

        var publishing = targetStatus == PublicationStatuses.Published;
        var now = time.GetUtcNow().UtcDateTime;
        var applied = new List<VersionState>();
        foreach (var state in current)
        {
            var publishedAt = publishing ? now : state.PublishedAt;
            await stores.Get(state.Kind).SetStatusAsync(state.Id, targetStatus, publishedAt, cancellationToken);
            if (publishing)
                foreach (var handler in HandlersOf(state.Kind)) await handler.OnPublishedAsync(state.Id, cancellationToken);
            applied.Add(state with { Status = targetStatus, PublishedAt = publishedAt });
        }
        if (publishing && current.Any(state => VersionKinds.Living.Contains(state.Kind))) await semanticRevision.AdvanceAsync(cancellationToken);

        return new TransitionResult { Outcome = TransitionOutcome.Applied, States = applied, Violations = [] };
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
        return CheckAsync([new VersionReference { Kind = kind, Id = id }], cancellationToken);
    }

    /// <summary>Runs the publication rules of a set of versions as their transition out of draft would, and changes nothing.</summary>
    /// <param name="versions">The versions that move together.</param>
    /// <param name="cancellationToken">Cancels the check between rules.</param>
    /// <returns>Every version's violations; empty when the set passes.</returns>
    public async Task<IReadOnlyList<RuleViolation>> CheckAsync(IReadOnlyList<VersionReference> versions, CancellationToken cancellationToken)
    {
        return await EvaluateRulesAsync(publicationRules, await StatesAsync(versions, cancellationToken), cancellationToken);
    }

    /// <summary>The current state of each version, in the order given.</summary>
    private async Task<IReadOnlyList<VersionState>> StatesAsync(IReadOnlyList<VersionReference> versions, CancellationToken cancellationToken)
    {
        var states = new List<VersionState>();
        foreach (var version in versions) states.Add(await stores.Get(version.Kind).GetAsync(version.Id, cancellationToken));
        return states;
    }

    /// <summary>The lifecycle handlers of a kind, in registration order.</summary>
    private IEnumerable<IVersionLifecycleHandler> HandlersOf(string kind)
    {
        return handlers.Where(handler => handler.Kind == kind);
    }

    /// <summary>
    /// Runs every rule of each version's kind, version by version in the order given and each kind's rules in registration order,
    /// with each version of a living kind counted as its identity's published version, and collects the violations.
    /// </summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateRulesAsync(
        IEnumerable<IVersionRule> rules,
        IReadOnlyList<VersionState> versions,
        CancellationToken cancellationToken)
    {
        foreach (var version in versions.Where(version => VersionKinds.Living.Contains(version.Kind)))
            publicationSet.Add(version.Kind, await stores.GetLiving(version.Kind).IdentityIdAsync(version.Id, cancellationToken), version.Id);
        var violations = new List<RuleViolation>();
        foreach (var version in versions)
            foreach (var rule in rules.Where(rule => rule.Kind == version.Kind)) violations.AddRange(await rule.EvaluateAsync(version.Id, cancellationToken));
        publicationSet.Clear();
        return violations;
    }
}

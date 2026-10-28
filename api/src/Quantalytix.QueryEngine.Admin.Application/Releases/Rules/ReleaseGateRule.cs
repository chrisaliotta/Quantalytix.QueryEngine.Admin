using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The release gates as the change-set pipeline's gate: a manifest edit that pins a foreign or unpublished version is refused (422).</summary>
/// <param name="rules">Runs the release rules.</param>
public sealed class ReleaseGateRule(ReleaseRules rules) : ISnapshotRule<ReleaseSnapshot>
{
    /// <summary>Gate.</summary>
    public RuleStage Stage => RuleStage.Gate;

    /// <summary>Runs the gate-stage release rules.</summary>
    /// <param name="snapshot">The pending release.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        return [.. (await rules.EvaluateAsync(snapshot, RuleStage.Gate, cancellationToken)).Select(violation => violation.ToRuleViolation())];
    }
}

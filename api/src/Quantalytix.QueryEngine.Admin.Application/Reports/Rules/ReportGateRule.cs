using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report gate (P2): the engine reads an enabled report as it stands, so a change set that leaves an enabled report breaking any
/// report rule is refused with 422, in preview and commit alike. A disabled report takes any change except one that leaves a row
/// naming a style the report lacks or a default profile it does not have, the references a delete cascade cannot repair (OI-56).
/// </summary>
/// <param name="rules">Runs every report rule.</param>
public sealed class ReportGateRule(ReportRules rules) : ISnapshotRule<ReportSnapshot>
{
    /// <summary>A gate.</summary>
    public RuleStage Stage => RuleStage.Gate;

    /// <summary>Every report rule's violations of an enabled report; a disabled report's unresolved style and profile references.</summary>
    /// <param name="snapshot">The report as the change set leaves it.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The violations that refuse the change set.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ReportSnapshot snapshot, CancellationToken cancellationToken)
    {
        var report = snapshot.Content;
        if (report.Report.IsEnabled) return await rules.EvaluateAsync([report], cancellationToken);
        return [.. ReportStylesResolveRule.Unresolved(report), .. DefaultProfileEnabledRule.Unresolved(report)];
    }
}

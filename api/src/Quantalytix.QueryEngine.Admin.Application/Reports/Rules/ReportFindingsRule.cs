using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Lists every report rule's violations of a disabled report as a preview's findings, which keep it from being enabled but not from
/// being saved. An enabled report has none: <see cref="ReportGateRule"/> refuses them.
/// </summary>
/// <param name="rules">Runs every report rule.</param>
public sealed class ReportFindingsRule(ReportRules rules) : ISnapshotRule<ReportSnapshot>
{
    /// <summary>Publication: findings that do not block saving.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>Every violation of a disabled pending report.</summary>
    /// <param name="snapshot">The report as the change set leaves it.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The violations; none for an enabled report.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ReportSnapshot snapshot, CancellationToken cancellationToken)
    {
        return snapshot.Content.Report.IsEnabled ? [] : await rules.EvaluateAsync([snapshot.Content], cancellationToken);
    }
}

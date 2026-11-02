using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Lists every report rule's violations of a disabled report as a preview's findings, which refuse its commit as well, because every
/// live version must restore cleanly. An enabled report has none: <see cref="ReportGateRule"/> refuses them.
/// </summary>
/// <param name="rules">Runs every report rule.</param>
public sealed class ReportFindingsRule(ReportRules rules) : ISnapshotRule<ReportSnapshot>
{
    /// <summary>Commit: findings refuse the save.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>Every violation of a disabled pending report.</summary>
    /// <param name="snapshot">The report as the change set leaves it.</param>
    /// <param name="cancellationToken">Cancels the rules' reads.</param>
    /// <returns>The violations; none for an enabled report.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ReportSnapshot snapshot, CancellationToken cancellationToken)
    {
        return snapshot.Content.Version.IsEnabled ? [] : await rules.EvaluateAsync([snapshot.Content], cancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Checks every enabled report against a stored release's dataset versions through the reports module's rules (R12), because a
/// report runs on whichever release is active. Report findings name no manifest row, and the report rules give no target, so each
/// finding targets its report.
/// </summary>
/// <param name="reader">Reads the enabled reports and their scope in a release.</param>
/// <param name="rules">The report rules, in registration order.</param>
public sealed class ReleaseReportChecks(ReportGraphReader reader, IEnumerable<IReportRule> rules)
{
    /// <summary>The release rules over reports, <c>report_sources_resolve</c> and <c>report_factors_apply</c>, which check and validate run.</summary>
    /// <param name="releaseId">The stored release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, rule by rule.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> ConsumersAsync(long releaseId, CancellationToken cancellationToken)
    {
        return EvaluateAsync(releaseId, [.. rules.Where(rule => rule is ReportSourcesResolveRule or ReportFactorsApplyRule)], cancellationToken);
    }

    /// <summary>Every report rule, which activation runs before the release reaches the reports.</summary>
    /// <param name="releaseId">The stored release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, rule by rule.</returns>
    public Task<IReadOnlyList<ReleaseViolation>> AllAsync(long releaseId, CancellationToken cancellationToken)
    {
        return EvaluateAsync(releaseId, [.. rules], cancellationToken);
    }

    /// <summary>Runs some report rules on every enabled report in the release's scope.</summary>
    private async Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(long releaseId, IReadOnlyList<IReportRule> applied, CancellationToken cancellationToken)
    {
        var reports = await reader.EnabledAsync(cancellationToken);
        var scope = await reader.ScopeAsync(reports, releaseId, cancellationToken);
        return
        [
            .. applied.SelectMany(rule => reports.SelectMany(report => rule.Evaluate(report, scope).Select(violation => new ReleaseViolation
            {
                Code = violation.Code,
                Message = violation.Message,
                DatasetId = null,
                Target = ReleaseFindings.Target(ReleaseTargetAreas.Report, report.Report.ReportId)
            })))
        ];
    }
}

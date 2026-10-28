using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Checks every enabled report against a release's dataset versions, stored or pending, through the reports module's rules (R12),
/// because a report runs on whichever release is active. Report findings name no manifest row, so each finding targets its report,
/// with the source's key when the rule names a source.
/// </summary>
/// <param name="reader">Reads the enabled reports and their scope in a release.</param>
/// <param name="rules">The report rules, in registration order.</param>
public sealed class ReleaseReportChecks(ReportGraphReader reader, IEnumerable<IReportRule> rules)
{
    /// <summary>The release rules over reports, <c>report_sources_resolve</c> and <c>report_factors_apply</c>, which check and validate run.</summary>
    /// <param name="releaseId">The stored release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, rule by rule.</returns>
    public async Task<IReadOnlyList<ReleaseViolation>> ConsumersAsync(long releaseId, CancellationToken cancellationToken)
    {
        var reports = await reader.EnabledAsync(cancellationToken);
        return Evaluate(reports, await reader.ScopeAsync(reports, releaseId, cancellationToken), Consumers());
    }

    /// <summary>The release rules over reports, over a manifest that is not stored, such as a release change set's pending one.</summary>
    /// <param name="versionsByDataset">The pending manifest: the pinned version of each dataset, by dataset id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, rule by rule.</returns>
    public async Task<IReadOnlyList<ReleaseViolation>> PendingConsumersAsync(IReadOnlyDictionary<long, long> versionsByDataset, CancellationToken cancellationToken)
    {
        var reports = await reader.EnabledAsync(cancellationToken);
        return Evaluate(reports, await reader.PendingScopeAsync(reports, versionsByDataset, cancellationToken), Consumers());
    }

    /// <summary>Every report rule, which activation runs before the release reaches the reports.</summary>
    /// <param name="releaseId">The stored release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, rule by rule.</returns>
    public async Task<IReadOnlyList<ReleaseViolation>> AllAsync(long releaseId, CancellationToken cancellationToken)
    {
        var reports = await reader.EnabledAsync(cancellationToken);
        return Evaluate(reports, await reader.ScopeAsync(reports, releaseId, cancellationToken), [.. rules]);
    }

    /// <summary>The report rules that read what a release gives reports.</summary>
    private IReadOnlyList<IReportRule> Consumers()
    {
        return [.. rules.Where(rule => rule is ReportSourcesResolveRule or ReportFactorsApplyRule)];
    }

    /// <summary>Runs some report rules on every enabled report in a scope.</summary>
    private static IReadOnlyList<ReleaseViolation> Evaluate(IReadOnlyList<ReportGraph> reports, ReportScope scope, IReadOnlyList<IReportRule> applied)
    {
        return
        [
            .. applied.SelectMany(rule => reports.SelectMany(report => rule.Evaluate(report, scope).Select(violation => new ReleaseViolation
            {
                Code = violation.Code,
                Message = violation.Message,
                DatasetId = null,
                Target = ReleaseFindings.Target(
                    ReleaseTargetAreas.Report, report.Report.ReportId, violation.Target is { Area: ReportAreas.Sources } target ? target.ItemKey : null)
            })))
        ];
    }
}

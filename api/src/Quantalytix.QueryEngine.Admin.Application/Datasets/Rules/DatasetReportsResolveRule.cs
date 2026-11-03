using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The report rules over the dataset version: every enabled report of the version's snapshot with a source on the dataset must still
/// resolve its sources (<c>report_sources_resolve</c>) and apply its bridge factors (<c>report_factors_apply</c>) against the version
/// as the change set leaves it, because the engine runs those reports on it at once. Each finding targets the settings tab with the
/// report's key.
/// </summary>
/// <param name="reader">Reads the reports and their scope with the pending version in place.</param>
/// <param name="rules">The report rules, of which the two that read datasets run.</param>
public sealed class DatasetReportsResolveRule(ReportGraphReader reader, IEnumerable<IReportRule> rules) : ISnapshotRule<DatasetSnapshot>
{
    /// <summary>Commit: a report the engine serves would break.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>Runs the dataset-reading report rules on every enabled report over the dataset.</summary>
    /// <param name="snapshot">The dataset version, stored or pending.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings, report by report.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DatasetSnapshot snapshot, CancellationToken cancellationToken)
    {
        var snapshotId = snapshot.Version.SnapshotId;
        var datasetId = snapshot.Dataset.DatasetId;
        var reports = await reader.EnabledReadingAsync(snapshotId, datasetId, cancellationToken);
        if (reports.Count == 0) return [];

        var pending = HeldDatasetVersion.Of(snapshot.Dataset.DatasetKey, snapshot.Fields, snapshot.Bindings, snapshot.TimeBindings, snapshot.Measures);
        var scope = await reader.PendingScopeAsync(reports, snapshotId, datasetId, pending, cancellationToken);
        IReportRule[] reading = [.. rules.Where(rule => rule is ReportSourcesResolveRule or ReportFactorsApplyRule)];
        return
        [
            .. reports.SelectMany(report => reading.SelectMany(rule => rule.Evaluate(report, scope)).Select(violation => new RuleViolation
            {
                Code = violation.Code,
                Message = violation.Message,
                Target = new ViolationTarget { Area = DatasetTabs.Settings, ItemKey = report.ReportKey }
            }))
        ];
    }
}

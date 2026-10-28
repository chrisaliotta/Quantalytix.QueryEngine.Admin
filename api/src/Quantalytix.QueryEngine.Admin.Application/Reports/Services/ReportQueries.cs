using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The report reads: the library, a report's document, its diagnostics, and a source's unmapped items.</summary>
/// <param name="reports">The report table.</param>
/// <param name="models">The model table.</param>
/// <param name="profiles">The profile table.</param>
/// <param name="sources">The source table.</param>
/// <param name="loader">Reads a report.</param>
/// <param name="rules">Runs the report rules.</param>
/// <param name="diagnostics">Computes diagnostics.</param>
public sealed class ReportQueries(
    ITableGateway<ReportRecord> reports,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<ReportSourceRecord> sources,
    ReportSnapshotLoader loader,
    ReportRules rules,
    ReportDiagnosticsBuilder diagnostics)
{
    /// <summary>Lists every report.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summaries by key.</returns>
    public async Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var modelKeys = (await models.ListAsync(null, cancellationToken)).ToDictionary(model => model.CalculationModelId, model => model.ModelKey);
        var profileCounts = (await profiles.ListAsync(null, cancellationToken)).CountBy(profile => profile.ReportId).ToDictionary();
        var sourceCounts = (await sources.ListAsync(null, cancellationToken)).CountBy(source => source.ReportId).ToDictionary();
        return
        [
            .. (await reports.ListAsync(null, cancellationToken))
                .OrderBy(report => report.ReportKey, StringComparer.Ordinal)
                .Select(report => ReportReadModels.Summary(
                    report, modelKeys[report.CalculationModelId], profileCounts.GetValueOrDefault(report.ReportId), sourceCounts.GetValueOrDefault(report.ReportId)))
        ];
    }

    /// <summary>A report's summary.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summary.</returns>
    public async Task<ReportSummary> SummaryAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        var owner = new { ReportId = reportId };
        return ReportReadModels.Summary(
            report,
            (await models.GetAsync(new { report.CalculationModelId }, cancellationToken)).ModelKey,
            (await profiles.ListAsync(owner, cancellationToken)).Count,
            (await sources.ListAsync(owner, cancellationToken)).Count);
    }

    /// <summary>A report's whole document.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document as stored.</returns>
    public async Task<ReportDocument> DocumentAsync(long reportId, CancellationToken cancellationToken)
    {
        return ReportReadModels.Document(await loader.LoadAsync(reportId, cancellationToken));
    }

    /// <summary>The diagnostics of a report as stored.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The diagnostics.</returns>
    public async Task<ReportDiagnostics> DiagnosticsAsync(long reportId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(reportId, cancellationToken);
        return await diagnostics.BuildAsync(ReportReadModels.Document(snapshot), await rules.EvaluateAsync([snapshot.Content], cancellationToken), cancellationToken);
    }

    /// <summary>The items a stored source reads that reach no row and are not left out.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceKey">The source.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The item keys; null when the report has no such source.</returns>
    public async Task<IReadOnlyList<string>?> UnmappedAsync(long reportId, string sourceKey, CancellationToken cancellationToken)
    {
        var source = (await DocumentAsync(reportId, cancellationToken)).Sources.FirstOrDefault(candidate => candidate.Key == sourceKey);
        return source is null ? null : (await diagnostics.CoverageAsync(source, cancellationToken)).Unmapped;
    }
}

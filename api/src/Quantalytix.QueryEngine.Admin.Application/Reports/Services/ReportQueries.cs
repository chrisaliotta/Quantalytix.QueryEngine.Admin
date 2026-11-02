using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report reads: the library of the reports the live snapshot holds, a report's live document or any version's, its diagnostics,
/// and a source's unmapped items.
/// </summary>
/// <param name="reports">Reads reports with their versions.</param>
/// <param name="models">The model table.</param>
/// <param name="profiles">The profile table.</param>
/// <param name="sources">The source table.</param>
/// <param name="live">The live snapshot.</param>
/// <param name="loader">Reads a report.</param>
/// <param name="rules">Runs the report rules.</param>
/// <param name="diagnostics">Computes diagnostics.</param>
public sealed class ReportQueries(
    ReportVersionReader reports,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<ReportSourceRecord> sources,
    LiveSnapshot live,
    ReportSnapshotLoader loader,
    ReportRules rules,
    ReportDiagnosticsBuilder diagnostics)
{
    /// <summary>Lists every report the live snapshot holds.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summaries by key.</returns>
    public async Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var listed = await reports.InSnapshotAsync(await live.IdAsync(cancellationToken), null, cancellationToken);
        var versionIds = listed.Select(report => report.Version.ReportVersionId).ToArray();
        var modelKeys = (await models.ListAsync(null, cancellationToken)).ToDictionary(model => model.CalculationModelId, model => model.ModelKey);
        var profileCounts = (await profiles.ListAsync(new { ReportVersionId = versionIds }, cancellationToken)).CountBy(profile => profile.ReportVersionId).ToDictionary();
        var sourceCounts = (await sources.ListAsync(new { ReportVersionId = versionIds }, cancellationToken)).CountBy(source => source.ReportVersionId).ToDictionary();
        return
        [
            .. listed.Select(report => ReportReadModels.Summary(
                report,
                modelKeys[report.Version.CalculationModelId],
                profileCounts.GetValueOrDefault(report.Version.ReportVersionId),
                sourceCounts.GetValueOrDefault(report.Version.ReportVersionId)))
        ];
    }

    /// <summary>A report's live summary.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summary.</returns>
    public async Task<ReportSummary> SummaryAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.LiveAsync(reportId, cancellationToken);
        var owner = new { report.Version.ReportVersionId };
        return ReportReadModels.Summary(
            report,
            (await models.GetAsync(new { report.Version.CalculationModelId }, cancellationToken)).ModelKey,
            (await profiles.ListAsync(owner, cancellationToken)).Count,
            (await sources.ListAsync(owner, cancellationToken)).Count);
    }

    /// <summary>A report's whole live document.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document as stored.</returns>
    public async Task<ReportDocument> DocumentAsync(long reportId, CancellationToken cancellationToken)
    {
        return ReportReadModels.Document(await loader.LoadAsync(reportId, cancellationToken));
    }

    /// <summary>A report version's whole document, live or frozen.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The document as stored.</returns>
    public async Task<ReportDocument> VersionDocumentAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        return ReportReadModels.Document(await loader.LoadVersionAsync(reportVersionId, cancellationToken));
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
        var document = await DocumentAsync(reportId, cancellationToken);
        var source = document.Sources.FirstOrDefault(candidate => candidate.Key == sourceKey);
        return source is null ? null : (await diagnostics.CoverageAsync(document, source, cancellationToken)).Unmapped;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Creates and deletes a report's draft together with the drafts of the objects the report owns: its report dimension, whose
/// draft holds the draft's rows, and every bridge into it, whose drafts hold the draft's row mappings. A report has at most one
/// draft, so its draft pairs with the one draft of each owned object, and the set validates and publishes together.
/// </summary>
public sealed class ReportDesignDrafts(
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ReportVersionService reportVersions,
    DimensionVersionService dimensionVersionService,
    BridgeVersionService bridgeVersionService,
    BridgeService bridgeService)
{
    /// <summary>
    /// Creates the report's draft, then, for the report dimension and every bridge into it that has no draft, a draft that copies
    /// its published version. A new report's dimension already has its first draft.
    /// </summary>
    /// <param name="reportId">The report, which has no draft.</param>
    /// <param name="input">The new draft's model, defaults and optional clone source, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The report's new draft.</returns>
    public async Task<ReportVersionRecord> CreateAsync(long reportId, NewReportVersionInput input, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        var draft = await reportVersions.CreateAsync(reportId, input, cancellationToken);
        var structure = await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken);
        if (structure.All(version => version.Status != PublicationStatuses.Draft))
        {
            var published = structure.Single(version => version.Status == PublicationStatuses.Published);
            await dimensionVersionService.CreateAsync(
                report.ReportDimensionId, new NewDimensionVersionInput { CloneFromVersionId = published.DimensionVersionId }, cancellationToken);
        }

        foreach (var bridge in await bridges.ListAsync(new { TargetDimensionId = report.ReportDimensionId }, cancellationToken))
        {
            var ownVersions = await bridgeVersions.ListAsync(new { bridge.BridgeId }, cancellationToken);
            if (ownVersions.Any(version => version.Status == PublicationStatuses.Draft)) continue;
            var published = ownVersions.Single(version => version.Status == PublicationStatuses.Published);
            var copy = new NewBridgeVersionInput
            {
                Cardinality = published.Cardinality,
                AllocationMode = published.AllocationMode,
                CloneFromVersionId = published.BridgeVersionId
            };
            await bridgeVersionService.CreateAsync(bridge.BridgeId, copy, cancellationToken);
        }
        return draft;
    }

    /// <summary>
    /// Deletes the report's draft with the drafts of its bridges, and a bridge that has no other version, because the draft
    /// created it. The report dimension's draft goes too when the dimension has a published version; otherwise it holds the rows
    /// of a report never published, which stay for its next draft.
    /// </summary>
    /// <param name="versionId">The report's draft.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the drafts are gone.</returns>
    public async Task DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        var draft = await reportVersions.GetAsync(versionId, cancellationToken);
        var report = await reports.GetAsync(new { draft.ReportId }, cancellationToken);
        await reportVersions.DeleteAsync(versionId, cancellationToken);

        var structure = await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken);
        if (structure.Any(version => version.Status == PublicationStatuses.Published))
        {
            foreach (var structureDraft in structure.Where(version => version.Status == PublicationStatuses.Draft))
                await dimensionVersionService.DeleteAsync(structureDraft.DimensionVersionId, cancellationToken);
        }

        foreach (var bridge in await bridges.ListAsync(new { TargetDimensionId = report.ReportDimensionId }, cancellationToken))
        {
            var ownVersions = await bridgeVersions.ListAsync(new { bridge.BridgeId }, cancellationToken);
            long[] drafts = [.. ownVersions.Where(version => version.Status == PublicationStatuses.Draft).Select(version => version.BridgeVersionId)];
            if (drafts.Length == ownVersions.Count) await bridgeService.RemoveAsync(bridge.BridgeId, drafts, cancellationToken);
            else await bridgeVersionService.RemoveAsync(drafts, cancellationToken);
        }
    }
}

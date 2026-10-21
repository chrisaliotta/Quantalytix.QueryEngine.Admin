using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads, creates, edits and deletes reports: the stable identities whose versions releases pin. Each report owns its report
/// dimension, which it creates, and every bridge into that dimension.
/// </summary>
public sealed class ReportService(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ReportVersionService versionService,
    DimensionService dimensionService,
    BridgeService bridgeService,
    PublishedDimensionReader publishedDimensions,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists every report.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reports by key, ignoring case.</returns>
    public async Task<IReadOnlyList<ReportRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await reports.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.ReportKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one report.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The report.</returns>
    public Task<ReportRecord> GetAsync(long reportId, CancellationToken cancellationToken)
    {
        return reports.GetAsync(new { ReportId = reportId }, cancellationToken);
    }

    /// <summary>
    /// Creates a report without versions, and first its own report dimension, keyed and named like the report, whose draft
    /// version 1 copies the published version of the dimension the report starts from, or holds a single root row.
    /// </summary>
    /// <param name="input">The new report, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored report.</returns>
    public async Task<ReportRecord> CreateAsync(NewReportInput input, CancellationToken cancellationToken)
    {
        long? templateVersionId = null;
        if (input.StartFromDimensionId is { } templateId)
            templateVersionId = (await publishedDimensions.ReadAsync(templateId, cancellationToken))!.Version.DimensionVersionId;
        var dimension = await dimensionService.CreateReportDimensionAsync(input.ReportKey, input.DisplayName, templateVersionId, cancellationToken);
        var report = new ReportRecord
        {
            ReportId = ids.NextId(),
            ReportKey = input.ReportKey,
            DisplayName = input.DisplayName,
            ReportType = input.ReportType,
            ReportDimensionId = dimension.DimensionId,
            IsEnabled = input.IsEnabled,
            PayloadJson = input.PayloadJson
        };
        await reports.InsertAsync(report, cancellationToken);
        return report;
    }

    /// <summary>
    /// Edits a report's identity attributes. A change advances the semantic revision, because the engine caches report
    /// definitions with their name, type, availability and payload.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored report.</returns>
    public async Task<ReportRecord> UpdateAsync(long reportId, ReportInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(reportId, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, ReportType = input.ReportType, IsEnabled = input.IsEnabled, PayloadJson = input.PayloadJson };
        await reports.UpdateAsync(updated, cancellationToken);
        if (updated != current) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a report whose versions are all drafts, with those drafts and their content, then the bridges into its report
    /// dimension and the dimension itself, whose versions are drafts too. A mapping set that maps into the report dimension
    /// blocks the delete, because the dimension goes with the report.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the report was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await GetAsync(reportId, cancellationToken);
        var reportVersions = await versions.ListAsync(new { ReportId = reportId }, cancellationToken);
        long[] versionIds = [.. reportVersions.Select(version => version.ReportVersionId)];
        var blockers = new DeleteBlockers($"Report '{report.ReportKey}'");
        blockers.AddNamed(
            "it has non-draft versions",
            reportVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        var mappingInto = await mappingSets.ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken);
        blockers.AddNamed("mapping sets map into its report dimension", mappingInto.Select(set => $"'{set.MappingSetKey}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await versionService.RemoveAsync(versionIds, cancellationToken);
        await reports.DeleteAsync(new { ReportId = reportId }, cancellationToken);
        foreach (var bridge in await bridges.ListAsync(new { TargetDimensionId = report.ReportDimensionId }, cancellationToken))
        {
            var ownVersions = await bridgeVersions.ListAsync(new { bridge.BridgeId }, cancellationToken);
            await bridgeService.RemoveAsync(bridge.BridgeId, [.. ownVersions.Select(version => version.BridgeVersionId)], cancellationToken);
        }
        await dimensionService.RemoveAsync(
            report.ReportDimensionId,
            await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken),
            cancellationToken);
        return null;
    }
}

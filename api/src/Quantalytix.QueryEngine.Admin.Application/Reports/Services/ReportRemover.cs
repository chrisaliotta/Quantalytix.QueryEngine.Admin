using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Deletes reports with everything they own (P12), once no frozen snapshot holds a version of them.</summary>
/// <param name="reports">Reads the report with its live version.</param>
/// <param name="versions">The report versions.</param>
/// <param name="sources">The source table.</param>
/// <param name="remover">Deletes the live version's content and the report.</param>
/// <param name="bridges">Deletes the sources' report bridges.</param>
/// <param name="reportDimensions">Removes the report dimension.</param>
/// <param name="dimensionVersions">Reads the report dimension's versions.</param>
/// <param name="holders">Names the frozen snapshots that hold the report.</param>
/// <param name="semanticRevision">The tenant's semantic revision.</param>
public sealed class ReportRemover(
    ReportVersionReader reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportSourceRecord> sources,
    ReportVersionRemover remover,
    ReportBridges bridges,
    ReportDimensions reportDimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    SnapshotHolders holders,
    SemanticRevision semanticRevision)
{
    /// <summary>
    /// Deletes a report with everything it owns: its live version's content, its sources' bridges, the report, then its report dimension.
    /// Nothing outside the report names what it owns, so only a frozen snapshot holding a version of it blocks the delete.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete, with nothing deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.LiveAsync(reportId, cancellationToken);
        var blockers = new DeleteBlockers($"Report '{report.ReportKey}'");
        blockers.AddFrozenSnapshots(await holders.FrozenKeysAsync((await versions.ListAsync(new { ReportId = reportId }, cancellationToken)).Select(row => row.SnapshotId), cancellationToken));
        if (blockers.Check() is { Allowed: false } check) return Refusal.DeleteBlocked(check);

        var bridgeIds = (await sources.ListAsync(new { report.Version.ReportVersionId }, cancellationToken)).Select(source => source.BridgeId).OfType<long>().ToList();
        await remover.RemoveVersionsAsync([report.Version.ReportVersionId], cancellationToken);
        foreach (var bridgeId in bridgeIds) await bridges.DeleteAsync(bridgeId, cancellationToken);
        await remover.RemoveIdentitiesAsync([reportId], cancellationToken);
        await reportDimensions.RemoveAsync(report.Report.ReportDimensionId, await dimensionVersions.ListAsync(new { DimensionId = report.Report.ReportDimensionId }, cancellationToken), cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }
}

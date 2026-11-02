using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the version of each object a report owns in one snapshot: the report dimension's, whose members are the report's member
/// rows, and each bridge source's bridge's, which holds the source's entries. The live ones are what its editor edits in place.
/// </summary>
public sealed class OwnedVersions(ITableGateway<DimensionVersionRecord> dimensionVersions, ITableGateway<BridgeVersionRecord> bridgeVersions)
{
    /// <summary>The report dimension's version in a snapshot.</summary>
    /// <param name="report">The report.</param>
    /// <param name="snapshotId">The snapshot of the report's version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version whose members are the member rows.</returns>
    public async Task<DimensionVersionRecord> StructureAsync(ReportRecord report, long snapshotId, CancellationToken cancellationToken)
    {
        return (await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId, SnapshotId = snapshotId }, cancellationToken)).Single();
    }

    /// <summary>The version of each bridge source's bridge in a snapshot.</summary>
    /// <param name="sources">Sources of a report version; direct sources have none.</param>
    /// <param name="snapshotId">The snapshot of the report's version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bridge versions, by source ID.</returns>
    public async Task<IReadOnlyDictionary<long, BridgeVersionRecord>> BridgesAsync(IEnumerable<ReportSourceRecord> sources, long snapshotId, CancellationToken cancellationToken)
    {
        var sourceOf = sources.Where(source => source.BridgeId is not null).ToDictionary(source => source.BridgeId!.Value, source => source.ReportSourceId);
        return (await bridgeVersions.ListAsync(new { BridgeId = sourceOf.Keys.ToArray(), SnapshotId = snapshotId }, cancellationToken)).ToDictionary(version => sourceOf[version.BridgeId]);
    }
}

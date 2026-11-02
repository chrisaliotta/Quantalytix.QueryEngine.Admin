using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// Reads which version every snapshot holds of every identity, the six kinds' version tables joined to their identities: the index
/// the snapshot reads, captures, restores and deletes work from.
/// </summary>
public sealed class SnapshotIndex(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions)
{
    /// <summary>Reads every version every snapshot holds.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The holdings: kind by kind in capture order, each by key.</returns>
    public async Task<IReadOnlyList<SnapshotHolding>> ReadAsync(CancellationToken cancellationToken)
    {
        var datasetRows = (await datasets.ListAsync(null, cancellationToken)).ToDictionary(row => row.DatasetId);
        var dimensionRows = (await dimensions.ListAsync(null, cancellationToken)).ToDictionary(row => row.DimensionId);
        var mappingSetRows = (await mappingSets.ListAsync(null, cancellationToken)).ToDictionary(row => row.MappingSetId);
        var bridgeRows = (await bridges.ListAsync(null, cancellationToken)).ToDictionary(row => row.BridgeId);
        var modelRows = (await models.ListAsync(null, cancellationToken)).ToDictionary(row => row.CalculationModelId);
        var reportRows = (await reports.ListAsync(null, cancellationToken)).ToDictionary(row => row.ReportId);
        IEnumerable<SnapshotHolding> holdings =
        [
            .. (await datasetVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.Dataset, row.DatasetId, datasetRows[row.DatasetId].DatasetKey, datasetRows[row.DatasetId].DisplayName, row.DatasetVersionId)),
            .. (await dimensionVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.Dimension, row.DimensionId, dimensionRows[row.DimensionId].DimensionKey, dimensionRows[row.DimensionId].DisplayName, row.DimensionVersionId)),
            .. (await mappingVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.MappingSet, row.MappingSetId, mappingSetRows[row.MappingSetId].MappingSetKey, mappingSetRows[row.MappingSetId].DisplayName, row.MappingVersionId)),
            .. (await bridgeVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.Bridge, row.BridgeId, bridgeRows[row.BridgeId].BridgeKey, bridgeRows[row.BridgeId].DisplayName, row.BridgeVersionId)),
            .. (await modelVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.CalculationModel, row.CalculationModelId, modelRows[row.CalculationModelId].ModelKey, row.DisplayName, row.CalculationModelVersionId)),
            .. (await reportVersions.ListAsync(null, cancellationToken)).Select(row => Holding(
                row.SnapshotId, SnapshotKinds.Report, row.ReportId, reportRows[row.ReportId].ReportKey, row.DisplayName, row.ReportVersionId))
        ];
        return [.. holdings.OrderBy(holding => KindOrder(holding.Entry.Kind)).ThenBy(holding => holding.Entry.Key, StringComparer.Ordinal)];
    }

    /// <summary>The entries one snapshot holds, kind by kind in capture order, each by key.</summary>
    /// <param name="holdings">Every holding.</param>
    /// <param name="snapshotId">The snapshot.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<SnapshotEntry> Of(IEnumerable<SnapshotHolding> holdings, long snapshotId)
    {
        return [.. holdings.Where(holding => holding.SnapshotId == snapshotId).Select(holding => holding.Entry)];
    }

    /// <summary>A kind's position in capture order.</summary>
    /// <param name="kind">A <c>SnapshotKind</c>.</param>
    /// <returns>Its index in <see cref="SnapshotKinds.All"/>.</returns>
    public static int KindOrder(string kind)
    {
        return SnapshotKinds.All.ToList().IndexOf(kind);
    }

    /// <summary>One holding.</summary>
    private static SnapshotHolding Holding(long snapshotId, string kind, long identityId, string key, string displayName, long versionId)
    {
        return new SnapshotHolding(snapshotId, new SnapshotEntry { Kind = kind, IdentityId = identityId, Key = key, DisplayName = displayName, VersionId = versionId });
    }
}

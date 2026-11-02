using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// Reads the <see cref="TenantCatalog"/> (blueprint 10, OI-13): one row per identity the live snapshot holds, with its live version,
/// the report that owns each report dimension and each bridge into one, and every snapshot.
/// </summary>
public sealed class TenantCatalogReader(
    TenantInfoReader info,
    LiveSnapshot live,
    ITableGateway<SnapshotRecord> snapshots,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions)
{
    /// <summary>Reads the catalog.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The catalog, each list by key.</returns>
    public async Task<TenantCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var held = new { SnapshotId = await live.IdAsync(cancellationToken) };
        var liveDatasets = (await datasetVersions.ListAsync(held, cancellationToken)).ToDictionary(version => version.DatasetId);
        var liveDimensions = (await dimensionVersions.ListAsync(held, cancellationToken)).ToDictionary(version => version.DimensionId);
        var liveMappingSets = (await mappingVersions.ListAsync(held, cancellationToken)).Select(version => version.MappingSetId).ToHashSet();
        var liveBridges = (await bridgeVersions.ListAsync(held, cancellationToken)).Select(version => version.BridgeId).ToHashSet();
        var liveModels = (await modelVersions.ListAsync(held, cancellationToken)).ToDictionary(version => version.CalculationModelId);
        var liveReports = (await reportVersions.ListAsync(held, cancellationToken)).ToDictionary(version => version.ReportId);
        var reportRows = (await reports.ListAsync(new { ReportId = liveReports.Keys.ToArray() }, cancellationToken)).OrderBy(row => row.ReportKey, StringComparer.Ordinal).ToList();
        var ownerOf = reportRows.ToDictionary(report => report.ReportDimensionId, report => report.ReportId);

        return new TenantCatalog
        {
            Tenant = await info.ReadAsync(cancellationToken),
            Datasets =
            [
                .. (await datasets.ListAsync(new { DatasetId = liveDatasets.Keys.ToArray() }, cancellationToken)).OrderBy(row => row.DatasetKey, StringComparer.Ordinal).Select(row => new TenantCatalogDataset
                {
                    Id = row.DatasetId,
                    Key = row.DatasetKey,
                    DisplayName = row.DisplayName,
                    IsEnabled = liveDatasets[row.DatasetId].IsEnabled,
                    LiveVersionId = liveDatasets[row.DatasetId].DatasetVersionId
                })
            ],
            Dimensions =
            [
                .. (await dimensions.ListAsync(new { DimensionId = liveDimensions.Keys.ToArray() }, cancellationToken)).OrderBy(row => row.DimensionKey, StringComparer.Ordinal).Select(row => new TenantCatalogDimension
                {
                    Id = row.DimensionId,
                    Key = row.DimensionKey,
                    DisplayName = row.DisplayName,
                    Kind = row.DimensionKind,
                    Purpose = row.DimensionPurpose,
                    Origin = row.DefinitionOrigin,
                    IsEnabled = row.IsEnabled,
                    LiveVersionId = liveDimensions[row.DimensionId].DimensionVersionId,
                    OwnerReportId = Owner(ownerOf, row.DimensionId)
                })
            ],
            Roles =
            [
                .. (await roles.ListAsync(new { DimensionId = liveDimensions.Keys.ToArray() }, cancellationToken)).OrderBy(row => row.DimensionRoleKey, StringComparer.Ordinal).Select(row => new TenantCatalogRole
                {
                    Id = row.DimensionRoleId,
                    Key = row.DimensionRoleKey,
                    DisplayName = row.DisplayName,
                    DimensionId = row.DimensionId
                })
            ],
            MappingSets =
            [
                .. (await mappingSets.ListAsync(new { MappingSetId = liveMappingSets.ToArray() }, cancellationToken)).OrderBy(row => row.MappingSetKey, StringComparer.Ordinal).Select(row => new TenantCatalogMappingSet
                {
                    Id = row.MappingSetId,
                    Key = row.MappingSetKey,
                    DisplayName = row.DisplayName,
                    DimensionId = row.DimensionId
                })
            ],
            Bridges =
            [
                .. (await bridges.ListAsync(new { BridgeId = liveBridges.ToArray() }, cancellationToken)).OrderBy(row => row.BridgeKey, StringComparer.Ordinal).Select(row => new TenantCatalogBridge
                {
                    Id = row.BridgeId,
                    Key = row.BridgeKey,
                    DisplayName = row.DisplayName,
                    SourceDimensionId = row.SourceDimensionId,
                    TargetDimensionId = row.TargetDimensionId,
                    OwnerReportId = Owner(ownerOf, row.TargetDimensionId)
                })
            ],
            Models =
            [
                .. (await models.ListAsync(new { CalculationModelId = liveModels.Keys.ToArray() }, cancellationToken)).OrderBy(row => row.ModelKey, StringComparer.Ordinal).Select(row => new TenantCatalogModel
                {
                    Id = row.CalculationModelId,
                    Key = row.ModelKey,
                    DisplayName = liveModels[row.CalculationModelId].DisplayName,
                    IsEnabled = liveModels[row.CalculationModelId].IsEnabled
                })
            ],
            Reports =
            [
                .. reportRows.Select(row => new TenantCatalogReport
                {
                    Id = row.ReportId,
                    Key = row.ReportKey,
                    DisplayName = liveReports[row.ReportId].DisplayName,
                    IsEnabled = liveReports[row.ReportId].IsEnabled,
                    ModelId = liveReports[row.ReportId].CalculationModelId,
                    ReportDimensionId = row.ReportDimensionId
                })
            ],
            Snapshots =
            [
                .. (await snapshots.ListAsync(null, cancellationToken))
                    .OrderByDescending(row => row.IsLive)
                    .ThenBy(row => row.SnapshotKey, StringComparer.Ordinal)
                    .Select(row => new TenantCatalogSnapshot { Id = row.SnapshotId, Key = row.SnapshotKey, DisplayName = row.DisplayName, IsLive = row.IsLive })
            ]
        };
    }

    /// <summary>The report that owns a dimension, or a bridge into it: a report owns its report dimension and every bridge into it.</summary>
    private static long? Owner(Dictionary<long, long> ownerOf, long dimensionId)
    {
        return ownerOf.TryGetValue(dimensionId, out var reportId) ? reportId : null;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// Reads the <see cref="TenantCatalog"/> (blueprint 10, OI-13): one row per identity of every module, with the live dimension
/// versions, the dataset versions the active release pins, and the report that owns each report dimension and each bridge into one.
/// </summary>
public sealed class TenantCatalogReader(
    TenantInfoReader info,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<ReportRecord> reports,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<AssumptionSetRecord> assumptionSets)
{
    /// <summary>Reads the catalog.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The catalog, each list by key.</returns>
    public async Task<TenantCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        var tenant = await info.ReadAsync(cancellationToken);
        var pinned = (await manifest.ListAsync(new { SemanticReleaseId = tenant.ActiveReleaseId }, cancellationToken))
            .ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        var live = (await dimensionVersions.ListAsync(new { Status = PublicationStatuses.Published }, cancellationToken))
            .ToDictionary(version => version.DimensionId);
        var reportRows = await reports.ListAsync(null, cancellationToken);
        var ownerOf = reportRows.ToDictionary(report => report.ReportDimensionId, report => report.ReportId);

        return new TenantCatalog
        {
            Tenant = tenant,
            Datasets =
            [
                .. (await datasets.ListAsync(null, cancellationToken)).OrderBy(row => row.DatasetKey, StringComparer.Ordinal).Select(row => new TenantCatalogDataset
                {
                    Id = row.DatasetId,
                    Key = row.DatasetKey,
                    DisplayName = row.DisplayName,
                    IsEnabled = row.IsEnabled,
                    ActiveVersionId = pinned.TryGetValue(row.DatasetId, out var versionId) ? versionId : null
                })
            ],
            Dimensions =
            [
                .. (await dimensions.ListAsync(null, cancellationToken)).OrderBy(row => row.DimensionKey, StringComparer.Ordinal).Select(row => new TenantCatalogDimension
                {
                    Id = row.DimensionId,
                    Key = row.DimensionKey,
                    DisplayName = row.DisplayName,
                    Kind = row.DimensionKind,
                    Purpose = row.DimensionPurpose,
                    Origin = row.DefinitionOrigin,
                    IsEnabled = row.IsEnabled,
                    PublishedVersionId = live.GetValueOrDefault(row.DimensionId)?.DimensionVersionId,
                    PublishedVersionNumber = live.GetValueOrDefault(row.DimensionId)?.VersionNumber,
                    OwnerReportId = Owner(ownerOf, row.DimensionId)
                })
            ],
            Roles =
            [
                .. (await roles.ListAsync(null, cancellationToken)).OrderBy(row => row.DimensionRoleKey, StringComparer.Ordinal).Select(row => new TenantCatalogRole
                {
                    Id = row.DimensionRoleId,
                    Key = row.DimensionRoleKey,
                    DisplayName = row.DisplayName,
                    DimensionId = row.DimensionId
                })
            ],
            MappingSets =
            [
                .. (await mappingSets.ListAsync(null, cancellationToken)).OrderBy(row => row.MappingSetKey, StringComparer.Ordinal).Select(row => new TenantCatalogMappingSet
                {
                    Id = row.MappingSetId,
                    Key = row.MappingSetKey,
                    DisplayName = row.DisplayName,
                    DimensionId = row.DimensionId
                })
            ],
            Bridges =
            [
                .. (await bridges.ListAsync(null, cancellationToken)).OrderBy(row => row.BridgeKey, StringComparer.Ordinal).Select(row => new TenantCatalogBridge
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
                .. (await models.ListAsync(null, cancellationToken)).OrderBy(row => row.ModelKey, StringComparer.Ordinal).Select(row => new TenantCatalogModel
                {
                    Id = row.CalculationModelId,
                    Key = row.ModelKey,
                    DisplayName = row.DisplayName,
                    IsEnabled = row.IsEnabled
                })
            ],
            Reports =
            [
                .. reportRows.OrderBy(row => row.ReportKey, StringComparer.Ordinal).Select(row => new TenantCatalogReport
                {
                    Id = row.ReportId,
                    Key = row.ReportKey,
                    DisplayName = row.DisplayName,
                    IsEnabled = row.IsEnabled,
                    ModelId = row.CalculationModelId,
                    ReportDimensionId = row.ReportDimensionId
                })
            ],
            Releases =
            [
                .. (await releases.ListAsync(null, cancellationToken)).OrderBy(row => row.ReleaseKey, StringComparer.Ordinal).Select(row => new TenantCatalogRelease
                {
                    Id = row.SemanticReleaseId,
                    Key = row.ReleaseKey,
                    Status = row.Status,
                    IsActive = row.SemanticReleaseId == tenant.ActiveReleaseId
                })
            ],
            AssumptionSets =
            [
                .. (await assumptionSets.ListAsync(null, cancellationToken)).OrderBy(row => row.AssumptionSetKey, StringComparer.Ordinal).Select(row => new TenantCatalogAssumptionSet
                {
                    Id = row.AssumptionSetId,
                    Key = row.AssumptionSetKey,
                    DisplayName = row.DisplayName,
                    Status = row.Status
                })
            ]
        };
    }

    /// <summary>The report that owns a dimension, or a bridge into it: a report owns its report dimension and every bridge into it.</summary>
    private static long? Owner(Dictionary<long, long> ownerOf, long dimensionId)
    {
        return ownerOf.TryGetValue(dimensionId, out var reportId) ? reportId : null;
    }
}

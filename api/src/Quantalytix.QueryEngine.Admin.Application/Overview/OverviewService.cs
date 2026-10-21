using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>Summarizes a tenant's metadata for the overview page.</summary>
public sealed class OverviewService(
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<AssumptionSetRecord> assumptionSets,
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<ReportRecord> reports)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Reads the tenant's identity, active release, counts and drafts.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The tenant overview.</returns>
    public async Task<TenantOverview> GetAsync(CancellationToken cancellationToken)
    {
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        var releaseRows = await releases.ListAsync(null, cancellationToken);
        var drafts = new { Status = PublicationStatuses.Draft };
        return new TenantOverview
        {
            TenantKey = info.TenantKey,
            MetadataSchemaVersion = info.MetadataSchemaVersion,
            SemanticRevision = info.SemanticRevision,
            ActiveRelease = releaseRows.SingleOrDefault(release => release.ReleaseId == info.ActiveReleaseId),
            Counts = new OverviewCounts
            {
                Datasets = (await datasets.ListAsync(null, cancellationToken)).Count,
                Dimensions = (await dimensions.ListAsync(null, cancellationToken)).Count,
                MappingSets = (await mappingSets.ListAsync(null, cancellationToken)).Count,
                Bridges = (await bridges.ListAsync(null, cancellationToken)).Count,
                Releases = releaseRows.Count,
                AssumptionSets = (await assumptionSets.ListAsync(null, cancellationToken)).Count,
                CalculationModels = (await models.ListAsync(null, cancellationToken)).Count,
                Reports = (await reports.ListAsync(null, cancellationToken)).Count,
                ActiveMeasures = await CountActiveMeasuresAsync(info.ActiveReleaseId, cancellationToken)
            },
            Drafts = new OverviewDrafts
            {
                DatasetVersions = (await datasetVersions.ListAsync(drafts, cancellationToken)).Count,
                DimensionVersions = (await dimensionVersions.ListAsync(drafts, cancellationToken)).Count,
                MappingVersions = (await mappingVersions.ListAsync(drafts, cancellationToken)).Count,
                BridgeVersions = (await bridgeVersions.ListAsync(drafts, cancellationToken)).Count,
                Releases = releaseRows.Count(release => release.Status == PublicationStatuses.Draft)
            }
        };
    }

    /// <summary>Counts the measures of the dataset versions the active release pins; a tenant without an active release has none.</summary>
    private async Task<int> CountActiveMeasuresAsync(long? activeReleaseId, CancellationToken cancellationToken)
    {
        if (activeReleaseId is null) return 0;
        var rows = await manifest.ListAsync(new { ReleaseId = activeReleaseId.Value }, cancellationToken);
        var activeMeasures = await measures.ListAsync(new { DatasetVersionId = rows.Select(row => row.DatasetVersionId).ToArray() }, cancellationToken);
        return activeMeasures.Count;
    }
}

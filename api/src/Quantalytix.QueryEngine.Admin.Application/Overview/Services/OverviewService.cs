using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>Summarizes a tenant's metadata for the overview screen, counting the identities of its <see cref="TenantCatalog"/>.</summary>
public sealed class OverviewService(
    TenantCatalogReader catalogReader,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<MeasureRecord> measures)
{
    /// <summary>Reads the tenant's info, counts and drafts.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The tenant overview.</returns>
    public async Task<TenantOverview> GetAsync(CancellationToken cancellationToken)
    {
        var catalog = await catalogReader.ReadAsync(cancellationToken);
        var activeVersionIds = catalog.Datasets.Select(dataset => dataset.ActiveVersionId).OfType<long>().ToArray();
        var counts = new TenantOverviewCounts
        {
            Datasets = catalog.Datasets.Count,
            Dimensions = catalog.Dimensions.Count(dimension => dimension.Purpose == DimensionPurposes.Analytical),
            MappingSets = catalog.MappingSets.Count,
            Bridges = catalog.Bridges.Count(bridge => bridge.OwnerReportId is null),
            Releases = catalog.Releases.Count,
            AssumptionSets = catalog.AssumptionSets.Count,
            Models = catalog.Models.Count,
            Reports = catalog.Reports.Count,
            ActiveMeasures = (await measures.ListAsync(new { DatasetVersionId = activeVersionIds }, cancellationToken)).Count
        };
        var drafts = new TenantOverviewDrafts
        {
            DatasetVersions = (await datasetVersions.ListAsync(new { Status = PublicationStatuses.Draft }, cancellationToken)).Count,
            Releases = catalog.Releases.Count(release => release.Status == PublicationStatuses.Draft)
        };
        return new TenantOverview(catalog.Tenant, counts, drafts);
    }
}

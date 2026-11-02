using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Overview;

/// <summary>Summarizes a tenant's metadata for the overview screen, counting the identities of its <see cref="TenantCatalog"/>.</summary>
public sealed class OverviewService(TenantCatalogReader catalogReader, ITableGateway<MeasureRecord> measures)
{
    /// <summary>Reads the tenant's info and counts.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The tenant overview.</returns>
    public async Task<TenantOverview> GetAsync(CancellationToken cancellationToken)
    {
        var catalog = await catalogReader.ReadAsync(cancellationToken);
        var liveVersionIds = catalog.Datasets.Select(dataset => dataset.LiveVersionId).ToArray();
        var counts = new TenantOverviewCounts
        {
            Datasets = catalog.Datasets.Count,
            Dimensions = catalog.Dimensions.Count(dimension => dimension.Purpose == DimensionPurposes.Analytical),
            MappingSets = catalog.MappingSets.Count,
            Bridges = catalog.Bridges.Count(bridge => bridge.OwnerReportId is null),
            FrozenSnapshots = catalog.Snapshots.Count(snapshot => !snapshot.IsLive),
            Models = catalog.Models.Count,
            Reports = catalog.Reports.Count,
            LiveMeasures = (await measures.ListAsync(new { DatasetVersionId = liveVersionIds }, cancellationToken)).Count
        };
        return new TenantOverview(catalog.Tenant, counts);
    }
}

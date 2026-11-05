using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Tests.Tenancy;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Overview;

/// <summary>
/// Confirms the overview's numbers (blueprint 10): identities are those the live snapshot holds; dimensions and bridges count
/// analytical ones only, because report-owned rows are reached only from their report (OI-22); live measures are only those of the
/// live dataset versions, the ones queries that name no snapshot use; and frozen snapshots are counted.
/// </summary>
public sealed class OverviewTests
{
    /// <summary>The overview carries the tenant info and counts identities as the catalog lists them.</summary>
    /// <returns>A task that completes when the overview is checked.</returns>
    [Fact]
    public async Task CountsLiveAnalyticalIdentities()
    {
        var overview = await new TenantTables().Overview().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("sandbox", 20, 12L), (overview.TenantKey, overview.MetadataSchemaVersion, overview.SemanticRevision));
        Assert.Equal(
            new TenantOverviewCounts
            {
                Datasets = 3,
                Dimensions = 2,
                MappingSets = 1,
                Bridges = 1,
                FrozenSnapshots = 1,
                Models = 1,
                Reports = 1,
                LiveMeasures = 3
            },
            overview.Counts);
    }
}

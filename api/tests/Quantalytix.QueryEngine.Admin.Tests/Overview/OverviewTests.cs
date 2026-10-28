using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Tests.Tenancy;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Overview;

/// <summary>
/// Confirms the overview's numbers (blueprint 10): dimensions and bridges count analytical ones only, because report-owned rows
/// are reached only from their report (OI-22); active measures are only those of the dataset versions the active release pins;
/// drafts exist only for dataset versions and releases (OI-14). The overview is where an administrator sees what queries use and
/// what work is still in progress.
/// </summary>
public sealed class OverviewTests
{
    /// <summary>The overview carries the tenant info, counts identities as the catalog lists them and counts drafts per kind.</summary>
    /// <returns>A task that completes when the overview is checked.</returns>
    [Fact]
    public async Task CountsAnalyticalIdentitiesAndDrafts()
    {
        var overview = await new TenantTables().Overview().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("sandbox", 18, 12L, (long?)TenantTables.ActiveReleaseId), (overview.TenantKey, overview.MetadataSchemaVersion, overview.SemanticRevision, overview.ActiveReleaseId));
        Assert.Equal(
            new TenantOverviewCounts
            {
                Datasets = 3,
                Dimensions = 2,
                MappingSets = 1,
                Bridges = 1,
                Releases = 3,
                AssumptionSets = 2,
                Models = 1,
                Reports = 1,
                ActiveMeasures = 3
            },
            overview.Counts);
        Assert.Equal(new TenantOverviewDrafts { DatasetVersions = 2, Releases = 1 }, overview.Drafts);
    }

    /// <summary>Without an active release no dataset version is pinned, so no measure is active.</summary>
    /// <returns>A task that completes when the overview is checked.</returns>
    [Fact]
    public async Task NoActiveRelease_HasNoActiveMeasures()
    {
        var tables = new TenantTables();
        var info = tables.DatabaseInfo.All.Single();
        await tables.DatabaseInfo.UpdateAsync(info with { ActiveSemanticReleaseId = null }, TestContext.Current.CancellationToken);

        var overview = await tables.Overview().GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(overview.ActiveReleaseId);
        Assert.Equal(0, overview.Counts.ActiveMeasures);
    }
}

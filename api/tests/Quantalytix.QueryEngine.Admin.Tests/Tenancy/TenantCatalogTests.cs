using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// Confirms the tenant catalog (blueprint 10, OI-13) that replaces the per-module catalogs: it resolves what pickers and
/// cross-module links need from one read, namely the identities the live snapshot holds with their live versions, which report
/// owns a report dimension or a bridge into one (OI-22), and every snapshot.
/// </summary>
public sealed class TenantCatalogTests
{
    /// <summary>Each dataset carries its live version and that version's enabled flag.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Datasets_CarryTheirLiveVersion()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("deposits", 21L, true), ("fees", 31L, false), ("loans", 11L, true)],
            catalog.Datasets.Select(dataset => (dataset.Key, dataset.LiveVersionId, dataset.IsEnabled)));
    }

    /// <summary>A dimension carries its live version, not a frozen one; a report dimension names its report.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Dimensions_CarryTheirLiveVersionAndOwner()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("branch", 102L, (long?)null), ("income_statement_rows", 301L, 601L), ("region", 201L, null)],
            catalog.Dimensions.Select(dimension => (dimension.Key, dimension.LiveVersionId, dimension.OwnerReportId)));
    }

    /// <summary>A bridge into a report dimension is owned by that report; an analytical bridge has no owner.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Bridges_IntoAReportDimension_AreOwnedByTheReport()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("branch_to_region", (long?)null), ("region_to_rows", 601)], catalog.Bridges.Select(bridge => (bridge.Key, bridge.OwnerReportId)));
    }

    /// <summary>An identity only a frozen snapshot holds is not listed, because pickers offer only what live can reference.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task IdentitiesOnlyAFrozenSnapshotHolds_AreNotListed()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["core_branch"], catalog.MappingSets.Select(set => set.Key));
    }

    /// <summary>Snapshots list the live one first, then the frozen ones by key.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Snapshots_ListLiveFirst()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("live", true), ("fy2025_close", false)], catalog.Snapshots.Select(snapshot => (snapshot.Key, snapshot.IsLive)));
    }
}

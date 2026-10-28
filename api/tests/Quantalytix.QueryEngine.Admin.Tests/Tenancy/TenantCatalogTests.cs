using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// Confirms the tenant catalog (blueprint 10, OI-13) that replaces the per-module catalogs: it resolves what pickers and
/// cross-module links need from one read, namely each dataset's pinned version, each dimension's live version, which report owns
/// a report dimension or a bridge into one (OI-22), and which release is active.
/// </summary>
public sealed class TenantCatalogTests
{
    /// <summary>A dataset's active version is the one the active release pins; a dataset the release does not pin has none.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Datasets_CarryTheVersionTheActiveReleasePins()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("deposits", (long?)21), ("fees", null), ("loans", 11)], catalog.Datasets.Select(dataset => (dataset.Key, dataset.ActiveVersionId)));
    }

    /// <summary>A dimension's published version is its live version, not a retired one; a report dimension names its report.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Dimensions_CarryTheirLiveVersionAndOwner()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [("branch", (long?)102, (int?)2, (long?)null), ("income_statement_rows", 301, 1, 601), ("region", 201, 1, null)],
            catalog.Dimensions.Select(dimension => (dimension.Key, dimension.PublishedVersionId, dimension.PublishedVersionNumber, dimension.OwnerReportId)));
    }

    /// <summary>A bridge into a report dimension is owned by that report; an analytical bridge has no owner.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Bridges_IntoAReportDimension_AreOwnedByTheReport()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("branch_to_region", (long?)null), ("region_to_rows", 601)], catalog.Bridges.Select(bridge => (bridge.Key, bridge.OwnerReportId)));
    }

    /// <summary>Only the release <c>qe_database_info</c> points at is active, and the catalog carries the same tenant info.</summary>
    /// <returns>A task that completes when the catalog is checked.</returns>
    [Fact]
    public async Task Releases_MarkTheActiveOne()
    {
        var catalog = await new TenantTables().CatalogReader().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TenantTables.ActiveReleaseId, catalog.Tenant.ActiveReleaseId);
        Assert.Equal([TenantTables.ActiveReleaseId], catalog.Releases.Where(release => release.IsActive).Select(release => release.Id));
    }
}

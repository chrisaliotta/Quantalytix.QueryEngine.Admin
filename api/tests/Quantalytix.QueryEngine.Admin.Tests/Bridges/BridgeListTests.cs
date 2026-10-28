using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms how the Bridges sections find bridges and tell report bridges from analytical ones (B15, OI-31). The list at a dimension
/// holds the bridges starting and ending there, analytical first, so a dimension shows both directions; a report bridge names the report
/// source that owns it, whose editor is the only place it changes; and <c>reportId</c> with <c>sourceKey</c> finds one source's bridge
/// (<c>findReportBridge</c>). Bindings name the field they classify and, for a bridge binding, the role of the binding it remaps.
/// </summary>
public sealed class BridgeListTests
{
    /// <summary>From branches, the analytical bridge comes before source <c>gl</c>'s report bridge; regions see only the bridge into them.</summary>
    /// <returns>A task that completes when the lists are checked.</returns>
    [Fact]
    public async Task Bridges_ListByDimension_AnalyticalFirst()
    {
        var queries = new MappingsFixture().BridgeQueries();

        var fromBranches = await queries.ListAsync(new BridgeListQuery { DimensionId = MappingsFixture.BranchDimensionId }, TestContext.Current.CancellationToken);
        var intoRegions = await queries.ListAsync(new BridgeListQuery { DimensionId = MappingsFixture.RegionDimensionId }, TestContext.Current.CancellationToken);

        Assert.Equal([MappingsFixture.BridgeId, MappingsFixture.ReportBridgeId], fromBranches.Select(bridge => bridge.Id));
        Assert.Null(fromBranches[0].Owner);
        Assert.Equal(new BridgeOwner { ReportId = ReportRows.ReportId, ReportKey = "balance_sheet", SourceKey = "gl" }, fromBranches[1].Owner);
        Assert.Equal([MappingsFixture.BridgeId], intoRegions.Select(bridge => bridge.Id));
    }

    /// <summary>A report and a source key find that source's bridge, and an unknown source finds none.</summary>
    /// <returns>A task that completes when the lists are checked.</returns>
    [Fact]
    public async Task ReportBridge_IsFoundByReportAndSourceKey()
    {
        var queries = new MappingsFixture().BridgeQueries();

        var found = await queries.ListAsync(new BridgeListQuery { ReportId = ReportRows.ReportId, SourceKey = "gl" }, TestContext.Current.CancellationToken);
        var none = await queries.ListAsync(new BridgeListQuery { ReportId = ReportRows.ReportId, SourceKey = "other" }, TestContext.Current.CancellationToken);

        Assert.Equal([MappingsFixture.ReportBridgeId], found.Select(bridge => bridge.Id));
        Assert.Empty(none);
    }

    /// <summary>A bridge binding classifies the field of the mapped binding it remaps and names that binding's role as its source role.</summary>
    /// <returns>A task that completes when the detail is checked.</returns>
    [Fact]
    public async Task BridgeBinding_NamesItsSourceFieldAndRole()
    {
        var fixture = new MappingsFixture();
        await fixture.BindAsync();

        var detail = await fixture.BridgeQueries().DetailAsync(MappingsFixture.BridgeId, TestContext.Current.CancellationToken);

        var usage = Assert.Single(detail.Bindings);
        Assert.Equal(
            new MappingBindingUsage
            {
                DatasetId = 10,
                DatasetKey = "bank.loans",
                DatasetVersionId = 1000,
                DatasetVersionNumber = 1,
                Status = "draft",
                FieldKey = "branch_code",
                RoleKey = "region",
                SourceRoleKey = "branch"
            },
            usage);
    }
}

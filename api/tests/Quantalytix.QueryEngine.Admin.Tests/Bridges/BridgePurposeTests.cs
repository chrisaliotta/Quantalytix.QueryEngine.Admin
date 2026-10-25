using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.ReportEditor;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms how a dimension's page finds its bridges and tells report bridges from analytical ones. The list names one dimension
/// and holds the bridges that lead into it and those that start from it, so the page shows both directions. A bridge into a report
/// dimension has purpose <c>report</c> and names the report source that owns it, whose editor is the only place it changes; every
/// other bridge is <c>analytical</c>, the kind queries remap dimensions with.
/// </summary>
public sealed class BridgePurposeTests
{
    /// <summary>
    /// From accounts, the list holds the source's report bridge, owned by source <c>gl</c>, and an analytical bridge into regions;
    /// regions see only the bridge into them.
    /// </summary>
    /// <returns>A task that completes when the lists are checked.</returns>
    [Fact]
    public async Task Bridges_ListByDimensionWithTheirPurpose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.Bridges.InsertAsync(BridgeRows.Bridge(92, AccountDimensionId, 600) with { BridgeKey = "account_to_region" }, cancellationToken);
        var bridges = fixture.BridgeService();

        var fromAccounts = await bridges.ListAsync(AccountDimensionId, cancellationToken);
        var intoRegions = Assert.Single(await bridges.ListAsync(600, cancellationToken));

        Assert.Equal([92L, BridgeId], fromAccounts.Select(summary => summary.Bridge.BridgeId));
        Assert.Equal((DimensionPurposes.Analytical, null), (fromAccounts[0].Purpose, fromAccounts[0].Owner));
        Assert.Equal(DimensionPurposes.Report, fromAccounts[1].Purpose);
        Assert.Equal(
            new BridgeOwner { ReportId = ReportRows.ReportId, ReportKey = "balance_sheet", ReportSourceId = SourceId, SourceKey = "gl" },
            fromAccounts[1].Owner);
        Assert.Equal(92, intoRegions.Bridge.BridgeId);
    }
}

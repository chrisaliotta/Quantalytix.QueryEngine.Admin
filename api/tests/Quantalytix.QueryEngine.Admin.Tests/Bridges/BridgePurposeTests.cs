using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.ReportEditor;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms how the Bridges section tells report bridges from analytical ones. A bridge into a report dimension has purpose
/// <c>report</c> and names the report source that owns it, whose editor is the only place it changes; every other bridge is
/// <c>analytical</c>, the kind queries remap dimensions with. The list filters by purpose, so analytical bridges can be reviewed
/// without the many bridges reports create.
/// </summary>
public sealed class BridgePurposeTests
{
    /// <summary>The source's bridge is a report bridge owned by source <c>gl</c>, and a bridge from accounts into regions is analytical.</summary>
    /// <returns>A task that completes when the lists are checked.</returns>
    [Fact]
    public async Task Bridges_CarryTheirPurposeAndFilterByIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.Bridges.InsertAsync(BridgeRows.Bridge(92, AccountDimensionId, 600) with { BridgeKey = "account_to_region" }, cancellationToken);
        var bridges = fixture.BridgeService();

        var all = await bridges.ListAsync(new BridgeListQuery(), cancellationToken);
        var report = Assert.Single(await bridges.ListAsync(new BridgeListQuery { Purpose = DimensionPurposes.Report }, cancellationToken));
        var analytical = Assert.Single(await bridges.ListAsync(new BridgeListQuery { Purpose = DimensionPurposes.Analytical }, cancellationToken));

        Assert.Equal(2, all.Count);
        Assert.Equal(BridgeId, report.Bridge.BridgeId);
        Assert.Equal(
            new BridgeOwner { ReportId = ReportRows.ReportId, ReportKey = "balance_sheet", ReportSourceId = SourceId, SourceKey = "gl" },
            report.Owner);
        Assert.Equal((92L, null), (analytical.Bridge.BridgeId, analytical.Owner));
    }
}

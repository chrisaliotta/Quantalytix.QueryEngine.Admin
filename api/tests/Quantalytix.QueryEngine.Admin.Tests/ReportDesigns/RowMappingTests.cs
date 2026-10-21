using Quantalytix.QueryEngine.Admin.Application.ReportDesigns;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportDesigns;

/// <summary>
/// Confirms that assigning a source item to a row moves it off the row it mapped to. A design source's bridge maps each item to at
/// most one row, because a second row would count the item's values twice, so the designer's "move here" is one replace of the
/// target row's items, and the row the item left keeps its other items.
/// </summary>
public sealed class RowMappingTests
{
    /// <summary>Listing account 1001, which maps to LOANS, among CASH's items maps it to CASH alone and empties LOANS.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task AssigningAnItemOfAnotherRow_MovesIt()
    {
        var fixture = new ReportDesignFixture(PublicationStatuses.Draft);

        var design = await fixture.RowMappings().ReplaceAsync(
            ReportRows.ReportVersionId,
            new RowMappingsInput { RowKey = "CASH", Mappings = [Item("1001", -1m), Item("1002", 1m)] },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [("1001", "CASH", -1m), ("1002", "CASH", 1m)],
            fixture.BridgeMappings.All.Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey, mapping.Factor)).Order());
        Assert.Empty(Assert.Single(design.Rows, row => row.Key == "LOANS").Mappings);
        Assert.Equal(["1001", "1002"], Assert.Single(design.Rows, row => row.Key == "CASH").Mappings.Select(mapping => mapping.ItemKey));
    }

    /// <summary>An item of the fixture's source with a factor.</summary>
    private static RowMappingInput Item(string itemKey, decimal factor)
    {
        return new RowMappingInput { BridgeId = ReportDesignFixture.BridgeId, ItemKey = itemKey, Factor = factor };
    }
}

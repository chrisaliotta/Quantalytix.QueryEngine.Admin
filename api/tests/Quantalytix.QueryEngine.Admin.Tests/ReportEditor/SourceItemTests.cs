using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms that listing an item for a row moves it off the row it mapped to. A bridge source's bridge maps each item to at most one
/// row, because a second row would count the item's values twice, so the editor's "move here" is one replace of the target row's
/// items, and the row the item left keeps its other items.
/// </summary>
public sealed class SourceItemTests
{
    /// <summary>Listing account 1001, which maps to LOANS, among CASH's items maps it to CASH alone and empties LOANS.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task ListingAnItemOfAnotherRow_MovesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();

        await fixture.Items().ReplaceAsync(
            ReportEditorFixture.SourceId,
            new SourceItemsInput { RowKey = "CASH", Items = [Item("1001", -1m), Item("1002", 1m)] },
            cancellationToken);

        var view = await fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken);
        Assert.Equal(
            [("1001", "CASH", -1m), ("1002", "CASH", 1m)],
            fixture.BridgeMappings.All.Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey, mapping.Factor)).Order());
        Assert.Empty(Assert.Single(view.Rows, row => row.Key == "LOANS").Items);
        Assert.Equal(["1001", "1002"], Assert.Single(view.Rows, row => row.Key == "CASH").Items.Select(item => item.ItemKey));
        Assert.Equal(1, Assert.Single(view.Sources).UnmappedCount);
    }

    /// <summary>An item with a factor.</summary>
    private static SourceItemInput Item(string itemKey, decimal factor)
    {
        return new SourceItemInput { ItemKey = itemKey, Factor = factor };
    }
}

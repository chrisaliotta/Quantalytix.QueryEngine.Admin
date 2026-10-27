using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms how a bridge source's rules add into rows. The source's bridge replicates items, so the rules of one row leave an
/// item's rules on other rows alone, and an item can add into several rows, each with its own factor. A rule can take a folder,
/// every item under a member including members added later, so the coverage counts the items under it as mapped. A rule that takes
/// an item alone needs an item that takes source values. On an enabled report a factor other than 1 must reach the report, so it is
/// refused while the source's measure ignores bridge factors, and nothing is written.
/// </summary>
public sealed class SourceItemTests
{
    /// <summary>CASH taking 1001 at -1 and 1002 leaves 1001 on LOANS, so 1001 adds into both rows.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task ItemOnAnotherRow_KeepsThatRule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();

        Assert.Empty(await fixture.Items().ReplaceAsync(
            ReportRows.ReportId, SourceId, new SourceItemsInput { RowKey = "CASH", Items = [Item("1001", -1m), Item("1002", 1m)] }, cancellationToken));

        var view = await fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken);
        Assert.Equal([("1001", "CASH", -1m), ("1001", "LOANS", 1m), ("1002", "CASH", 1m)], Mappings(fixture));
        Assert.Equal(["1001"], Assert.Single(view.Rows, row => row.Key == "LOANS").Items.Select(item => item.ItemKey));
        Assert.Equal(["1001", "1002"], Assert.Single(view.Rows, row => row.Key == "CASH").Items.Select(item => item.ItemKey));
    }

    /// <summary>A rule taking folder LENDING reaches 1001 and 1003 under it, so 1003, unmapped before, is mapped.</summary>
    /// <returns>A task that completes when the coverage is checked.</returns>
    [Fact]
    public async Task FolderRule_ReachesTheItemsUnderIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        Assert.Equal(["1003"], await fixture.Coverage().UnmappedAsync(SourceId, cancellationToken));

        Assert.Empty(await fixture.Items().ReplaceAsync(
            ReportRows.ReportId, SourceId, new SourceItemsInput { RowKey = "CASH", Items = [Folder("LENDING"), Item("1002", 1m)] }, cancellationToken));

        Assert.Empty(await fixture.Coverage().UnmappedAsync(SourceId, cancellationToken));
        var view = await fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken);
        Assert.Equal(0, Assert.Single(view.Sources).UnmappedCount);
        var folder = Assert.Single(Assert.Single(view.Rows, row => row.Key == "CASH").Items, item => item.ItemKey == "LENDING");
        Assert.Equal(SourceSelections.Descendants, folder.Selection);
    }

    /// <summary>Folder LENDING takes no source values of its own, so a rule takes every item under it, not LENDING alone.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task RuleTakingAnItemAlone_NeedsAnItemWithSourceValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var validator = new ReportEditorFixture().ItemsValidator();

        var refused = await validator.TestValidateAsync(
            new SourceItemsInput { RowKey = "CASH", Items = [Item("LENDING", 1m)] }, cancellationToken: cancellationToken);
        var passes = await validator.TestValidateAsync(
            new SourceItemsInput { RowKey = "CASH", Items = [Folder("LENDING")] }, cancellationToken: cancellationToken);

        refused.ShouldHaveValidationErrorFor(input => input.Items)
            .WithErrorMessage("Item 'LENDING' takes no source values of its own; take every item under it instead.");
        passes.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// On the enabled report, taking half of 1002 is refused while GL's balance ignores bridge factors, and taken once the measure
    /// applies them.
    /// </summary>
    /// <returns>A task that completes when the writes are checked.</returns>
    [Fact]
    public async Task FactorOnAnEnabledReport_NeedsAMeasureThatAppliesFactors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.SetEnabledAsync(true);
        var half = new SourceItemsInput { RowKey = "CASH", Items = [Item("1002", 0.5m)] };

        var violation = Assert.Single(await fixture.Items().ReplaceAsync(ReportRows.ReportId, SourceId, half, cancellationToken));
        Assert.Equal(ReportFactorsApplyRule.Code, violation.Code);
        Assert.Equal([("1001", "LOANS", 1m), ("1002", "CASH", 1m)], Mappings(fixture));

        var balance = Assert.Single(fixture.Measures.All);
        await fixture.Measures.UpdateAsync(balance with { BridgeFactorBehavior = BridgeFactorBehaviors.Value }, cancellationToken);
        Assert.Empty(await fixture.Items().ReplaceAsync(ReportRows.ReportId, SourceId, half, cancellationToken));
        Assert.Equal([("1001", "LOANS", 1m), ("1002", "CASH", 0.5m)], Mappings(fixture));
    }

    /// <summary>A rule taking one item alone, with a factor.</summary>
    private static SourceItemInput Item(string itemKey, decimal factor)
    {
        return new SourceItemInput { ItemKey = itemKey, Selection = SourceSelections.Member, Factor = factor };
    }

    /// <summary>A rule taking every item under a member, whole.</summary>
    private static SourceItemInput Folder(string itemKey)
    {
        return new SourceItemInput { ItemKey = itemKey, Selection = SourceSelections.Descendants, Factor = 1m };
    }

    /// <summary>The bridge's entries as (item, row, factor), in order.</summary>
    private static (string, string, decimal)[] Mappings(ReportEditorFixture fixture)
    {
        return [.. fixture.BridgeMappings.All.Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey, mapping.Factor)).Order()];
    }
}

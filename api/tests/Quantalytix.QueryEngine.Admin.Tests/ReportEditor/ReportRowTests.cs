using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms how a report's row tree is written and refused. Removing a row takes what names it with it: the bridge entries that map
/// items to it, its variable scopes and its member option, and the overlay rows the tree no longer lists under it; otherwise a
/// report rule would fail on a key that no longer resolves. New rows are created under their parents whatever order the list gives
/// them in. A tree the report dimension and the engine cannot hold is refused before anything is written, naming the problem; so is
/// a stored member row turned into an overlay row, which would orphan its key, and a row that takes items turned into a group,
/// whose items would vanish from the report.
/// </summary>
public sealed class ReportRowTests
{
    /// <summary>
    /// Removing LOANS, with the note under it, removes the entry that maps account 1001 to LOANS, the scope and member option on
    /// LOANS, the note and the member; CASH keeps its entry.
    /// </summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task RemovingARow_RemovesWhatNamesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.Scopes.InsertAsync(ReportRows.Scope("balance", "LOANS"), cancellationToken);
        await fixture.MemberOptions.InsertAsync(ReportRows.MemberOption("LOANS"), cancellationToken);
        await fixture.Lines.InsertAsync(ReportRows.Line(21, "NOTE", ReportLineKinds.Section, "LOANS", 0), cancellationToken);

        var violations = await fixture.Rows().ReplaceAsync(
            ReportRows.ReportId,
            new ReportRowsInput { Rows = [Row("TOTAL", ReportRowKinds.Group, null), Row("CASH", ReportRowKinds.Data, "TOTAL")] },
            cancellationToken);

        Assert.Empty(violations);
        Assert.Equal(["CASH", "TOTAL"], (await fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken)).Rows.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(fixture.Members.All, member => member.MemberKey == "LOANS");
        Assert.Equal([("1002", "CASH")], fixture.BridgeMappings.All.Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey)));
        Assert.Empty(fixture.Scopes.All);
        Assert.Empty(fixture.MemberOptions.All);
        Assert.Empty(fixture.Lines.All);
    }

    /// <summary>A new group listed after the new data row under it is still placed above it, and a header becomes a member option.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task NewRows_AreCreatedUnderTheirParents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();

        await fixture.Rows().ReplaceAsync(
            ReportRows.ReportId,
            new ReportRowsInput
            {
                Rows =
                [
                    Row("TOTAL", ReportRowKinds.Group, null),
                    Row("LOANS", ReportRowKinds.Data, "TOTAL"),
                    Row("CASH", ReportRowKinds.Data, "TOTAL"),
                    Row("MORTGAGE", ReportRowKinds.Data, "FUNDING"),
                    Row("FUNDING", ReportRowKinds.Group, "TOTAL") with { ShowsValues = false }
                ]
            },
            cancellationToken);

        var view = await fixture.Reader().ReadAsync(ReportRows.ReportId, cancellationToken);
        var mortgage = Assert.Single(view.Rows, row => row.Key == "MORTGAGE");
        var funding = Assert.Single(view.Rows, row => row.Key == "FUNDING");
        Assert.Equal(("FUNDING", ReportRowKinds.Data), (mortgage.ParentKey, mortgage.Kind));
        Assert.Equal((ReportRowKinds.Group, false), (funding.Kind, funding.ShowsValues));
        Assert.Equal(["FUNDING"], fixture.MemberOptions.All.Select(option => option.MemberKey));
    }

    /// <summary>Each tree the report dimension's hierarchy or the engine cannot hold names its problem.</summary>
    [Fact]
    public void MisshapenTrees_NameTheirProblem()
    {
        Assert.Equal(
            ["The report has 2 top-level member rows; it needs exactly one, the report's total."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("OTHER", ReportRowKinds.Group, null)]));
        Assert.Equal(
            ["Member row 'LOANS' sits under overlay row 'ASSETS'; member rows sit under member rows."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("ASSETS", ReportRowKinds.Section, "TOTAL"), Row("LOANS", ReportRowKinds.Data, "ASSETS")]));
        Assert.Equal(
            ["Row 'NOTE' sits under formula row 'YIELD', which has no rows under it."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Formula("YIELD", "TOTAL"), Row("NOTE", ReportRowKinds.Section, "YIELD")]));
        Assert.Equal(
            ["Row 'LOANS' sits under 'GONE', which is no row of the report."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Data, "GONE")]));
        Assert.Equal(
            ["Row key 'LOANS' appears more than once."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Data, "TOTAL"), Row("LOANS", ReportRowKinds.Data, "TOTAL")]));
        Assert.Equal(
            ["Row 'A' sits under itself through its parents.", "Row 'B' sits under itself through its parents."],
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("A", ReportRowKinds.Section, "B"), Row("B", ReportRowKinds.Section, "A")]));
    }

    /// <summary>
    /// A data row with member rows under it, as the seed MarginStatement's LOANS is (an assignable member with children that a
    /// bridge maps accounts to), is a tree the report holds, so a report copied from such a dimension stays editable.
    /// </summary>
    [Fact]
    public void DataRowsWithMemberRowsUnderThem_AreHeld()
    {
        Assert.Empty(
            ReportRowTree.Problems([Row("TOTAL", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Data, "TOTAL"), Row("AUTO", ReportRowKinds.Data, "LOANS")]));
    }

    /// <summary>
    /// The stored member row CASH cannot become a section, and LOANS and CASH, which take accounts 1001 and 1002, cannot stop being
    /// data rows; the unchanged tree passes.
    /// </summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task StoredRows_KeepTheirNature()
    {
        var validator = new ReportEditorFixture().RowsValidator();
        var cancellationToken = TestContext.Current.CancellationToken;
        ReportRowInput[] unchanged = [Row("TOTAL", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Data, "TOTAL"), Row("CASH", ReportRowKinds.Data, "TOTAL")];

        var passes = await validator.TestValidateAsync(new ReportRowsInput { Rows = unchanged }, cancellationToken: cancellationToken);
        var refused = await validator.TestValidateAsync(
            new ReportRowsInput
            {
                Rows = [Row("TOTAL", ReportRowKinds.Group, null), Row("LOANS", ReportRowKinds.Group, "TOTAL"), Row("CASH", ReportRowKinds.Section, "TOTAL")]
            },
            cancellationToken: cancellationToken);

        passes.ShouldNotHaveAnyValidationErrors();
        Assert.Equal(
            ["Row 'CASH' is a member of the report dimension, so it stays a data or group row.",
             "Row 'LOANS' takes items from the report's bridge sources, so it stays a data row; move its items first.",
             "Row 'CASH' takes items from the report's bridge sources, so it stays a data row; move its items first."],
            refused.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A visible row that shows its values, labelled with its key.</summary>
    private static ReportRowInput Row(string key, string kind, string? parentKey)
    {
        return new ReportRowInput { Key = key, Label = key, Kind = kind, ParentKey = parentKey, SortOrder = 0, IsVisible = true, ShowsValues = true };
    }

    /// <summary>A formula row that places calculated measure yield in column yield.</summary>
    private static ReportRowInput Formula(string key, string parentKey)
    {
        return Row(key, ReportRowKinds.Formula, parentKey) with { CalculatedMeasureKey = "yield", ValueColumnKey = "yield" };
    }
}

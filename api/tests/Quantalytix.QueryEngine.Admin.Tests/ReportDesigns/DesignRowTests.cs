using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.ReportDesigns;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportDesigns;

/// <summary>
/// Confirms how a report design's row tree is written and refused. Removing a row that takes source items deletes the bridge
/// entries that map to it before the member, because a published entry naming a missing row would block the design's next publish.
/// New rows are created under their parents whatever order the list gives them in. A tree the report dimension and the engine
/// cannot hold is refused before anything is written, naming the problem; so is a stored member row turned into an overlay row,
/// which would orphan its key, and a row that takes source items turned into a group, whose items would vanish from the report.
/// </summary>
public sealed class DesignRowTests
{
    /// <summary>Removing LOANS removes the entry that maps account 1001 to it, then the member; CASH keeps its entry.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task RemovingAMappedRow_RemovesItsEntriesBeforeTheMember()
    {
        var fixture = new ReportDesignFixture(PublicationStatuses.Draft);

        var design = await fixture.Rows().ReplaceAsync(
            ReportRows.ReportVersionId,
            new DesignRowsInput { Rows = [Row("TOTAL", DesignRowKinds.Group, null), Row("CASH", DesignRowKinds.Data, "TOTAL")] },
            TestContext.Current.CancellationToken);

        Assert.Equal(["CASH", "TOTAL"], design.Rows.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(fixture.Members.All, member => member.MemberKey == "LOANS");
        Assert.Equal([("1002", "CASH")], fixture.BridgeMappings.All.Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey)));
    }

    /// <summary>A new group listed after the new data row under it is still created first, and a header becomes a member option.</summary>
    /// <returns>A task that completes when the replace is checked.</returns>
    [Fact]
    public async Task NewRows_AreCreatedUnderTheirParents()
    {
        var fixture = new ReportDesignFixture(PublicationStatuses.Draft);

        var design = await fixture.Rows().ReplaceAsync(
            ReportRows.ReportVersionId,
            new DesignRowsInput
            {
                Rows =
                [
                    Row("TOTAL", DesignRowKinds.Group, null),
                    Row("LOANS", DesignRowKinds.Data, "TOTAL"),
                    Row("CASH", DesignRowKinds.Data, "TOTAL"),
                    Row("MORTGAGE", DesignRowKinds.Data, "FUNDING"),
                    Row("FUNDING", DesignRowKinds.Group, "TOTAL") with { ShowsValues = false }
                ]
            },
            TestContext.Current.CancellationToken);

        var mortgage = Assert.Single(design.Rows, row => row.Key == "MORTGAGE");
        var funding = Assert.Single(design.Rows, row => row.Key == "FUNDING");
        Assert.Equal(("FUNDING", DesignRowKinds.Data), (mortgage.ParentKey, mortgage.Kind));
        Assert.Equal((DesignRowKinds.Group, false), (funding.Kind, funding.ShowsValues));
        Assert.Equal(["FUNDING"], fixture.MemberOptions.All.Select(option => option.MemberKey));
    }

    /// <summary>Each tree the report dimension's hierarchy or the engine cannot hold names its problem.</summary>
    [Fact]
    public void MisshapenTrees_NameTheirProblem()
    {
        Assert.Equal(
            ["The design has 2 top-level member rows; it needs exactly one, the report's total."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("OTHER", DesignRowKinds.Group, null)]));
        Assert.Equal(
            ["Member row 'LOANS' sits under overlay row 'ASSETS'; member rows sit under member rows."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("ASSETS", DesignRowKinds.Section, "TOTAL"), Row("LOANS", DesignRowKinds.Data, "ASSETS")]));
        Assert.Equal(
            ["Row 'NOTE' sits under formula row 'YIELD', which has no rows under it."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Formula("YIELD", "TOTAL"), Row("NOTE", DesignRowKinds.Section, "YIELD")]));
        Assert.Equal(
            ["Row 'LOANS' sits under 'GONE', which is no row of the design."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("LOANS", DesignRowKinds.Data, "GONE")]));
        Assert.Equal(
            ["Row key 'LOANS' appears more than once."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("LOANS", DesignRowKinds.Data, "TOTAL"), Row("LOANS", DesignRowKinds.Data, "TOTAL")]));
        Assert.Equal(
            ["Row 'A' sits under itself through its parents.", "Row 'B' sits under itself through its parents."],
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("A", DesignRowKinds.Section, "B"), Row("B", DesignRowKinds.Section, "A")]));
    }

    /// <summary>
    /// A data row with member rows under it, as the seed MarginStatement's LOANS is (an assignable member with children that a
    /// bridge maps accounts to), is a tree the design holds, so a design copied from such a dimension stays editable.
    /// </summary>
    [Fact]
    public void DataRowsWithMemberRowsUnderThem_AreHeld()
    {
        Assert.Empty(
            DesignRowTree.Problems([Row("TOTAL", DesignRowKinds.Group, null), Row("LOANS", DesignRowKinds.Data, "TOTAL"), Row("AUTO", DesignRowKinds.Data, "LOANS")]));
    }

    /// <summary>
    /// The stored member row CASH cannot become a section, and LOANS and CASH, which take accounts 1001 and 1002, cannot stop being
    /// data rows; the unchanged tree passes.
    /// </summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task StoredRows_KeepTheirNature()
    {
        var validator = new ReportDesignFixture(PublicationStatuses.Draft).RowsValidator();
        var cancellationToken = TestContext.Current.CancellationToken;
        DesignRowInput[] unchanged = [Row("TOTAL", DesignRowKinds.Group, null), Row("LOANS", DesignRowKinds.Data, "TOTAL"), Row("CASH", DesignRowKinds.Data, "TOTAL")];

        var passes = await validator.TestValidateAsync(new DesignRowsInput { Rows = unchanged }, cancellationToken: cancellationToken);
        var refused = await validator.TestValidateAsync(
            new DesignRowsInput
            {
                Rows = [Row("TOTAL", DesignRowKinds.Group, null), Row("LOANS", DesignRowKinds.Group, "TOTAL"), Row("CASH", DesignRowKinds.Section, "TOTAL")]
            },
            cancellationToken: cancellationToken);

        passes.ShouldNotHaveAnyValidationErrors();
        Assert.Equal(
            ["Row 'CASH' is a member of the report dimension, so it stays a data or group row.",
             "Row 'LOANS' takes items from the design's sources, so it stays a data row; move its items first.",
             "Row 'CASH' takes items from the design's sources, so it stays a data row; move its items first."],
            refused.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A visible row that shows its values, labelled with its key.</summary>
    private static DesignRowInput Row(string key, string kind, string? parentKey)
    {
        return new DesignRowInput { Key = key, Label = key, Kind = kind, ParentKey = parentKey, SortOrder = 0, IsVisible = true, ShowsValues = true };
    }

    /// <summary>A formula row that places calculated measure yield in column yield.</summary>
    private static DesignRowInput Formula(string key, string parentKey)
    {
        return Row(key, DesignRowKinds.Formula, parentKey) with { CalculatedMeasureKey = "yield", ValueColumnKey = "yield" };
    }
}

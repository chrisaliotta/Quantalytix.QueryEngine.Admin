using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_items_resolve</c>: the report's member rows must be one tree, every member option and variable scope must
/// name a member row, every overlay row must sit under a member row or another overlay row, and no overlay row may take a member
/// row's key, because rows are keyed by both. The orchestrator lays the rows out from the report dimension's members and
/// evaluates formula rows at its root.
/// </summary>
public sealed class ReportItemsResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded report reads a tree with one root that holds its target, with a header option and overlay rows that resolve.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TreeWithTheNamedMembers_Passes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.MemberOptions.InsertAsync(MemberOption("total"), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(11, "notes", ReportLineKinds.Section, "assets", 0), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(12, "detail", ReportLineKinds.Section, "notes", 0), cancellationToken);

        Assert.Empty(await _fixture.EvaluateAsync(new ReportItemsResolveRule()));
    }

    /// <summary>
    /// A second root, an option for liabilities, a row under equity, a row keyed like the assets member and a scope at equity are each
    /// reported.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SecondRootMissingMembersAndKeyCollision_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Members.InsertAsync(DimensionRows.Member(3, 501, "memo_items"), cancellationToken);
        await _fixture.MemberOptions.InsertAsync(MemberOption("liabilities"), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(11, "notes", ReportLineKinds.Section, "equity", 0), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(12, "assets", ReportLineKinds.Section, null, 2), cancellationToken);
        await _fixture.Scopes.InsertAsync(Scope("balance", "equity"), cancellationToken);

        var violations = await _fixture.EvaluateAsync(new ReportItemsResolveRule());

        Assert.Equal(
            ["Report 'balance_sheet' has 2 top-level member rows; it needs one, the report's total.",
             "Report 'balance_sheet' has an option for row 'liabilities', which is no member row.",
             "Report 'balance_sheet' row 'notes' sits under 'equity', which is no row of the report.",
             "Report 'balance_sheet' row 'assets' has the key of a member row; rows are keyed by both.",
             "Report 'balance_sheet' scopes variable 'balance' to row 'equity', which is no member row."],
            violations.Select(violation => violation.Message));
    }
}

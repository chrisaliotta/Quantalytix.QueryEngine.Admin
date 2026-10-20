using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_items_resolve</c>: the report dimension's published version must have one root, every member option and
/// variable target must name one of its members, every overlay row must sit under one of its members or another overlay row, and no
/// overlay row may take a member's key, because rows are keyed by both. Sources classify into the version the active assumption set
/// pins; the orchestrator lays the rows out from its members and evaluates formula rows at its root.
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

        Assert.Empty(await EvaluateAsync(Rule(), ReportVersionId));
    }

    /// <summary>
    /// A second root, an option for liabilities, a row under equity, a row keyed like the assets member and a target at equity are each
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
        await _fixture.Targets.InsertAsync(Target("balance", "equity"), cancellationToken);

        var violations = await EvaluateAsync(Rule(), ReportVersionId);

        Assert.Equal(
            ["Report 'balance_sheet' reads a report dimension whose published version has 2 roots; it needs one.",
             "Report 'balance_sheet' has an option for member 'liabilities', which the published version of the report dimension lacks.",
             "Report 'balance_sheet' row 'notes' sits under 'equity', which is no overlay row and no member the published version of the report dimension has.",
             "Report 'balance_sheet' row 'assets' has the key of a member of the report dimension; rows are keyed by both.",
             "Report 'balance_sheet' targets variable 'balance' at member 'equity', which the published version of the report dimension lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The rule over the fixture's tables.</summary>
    private ReportItemsResolveRule Rule()
    {
        return new ReportItemsResolveRule(_fixture.Reader(), _fixture.PublishedDimensions());
    }
}

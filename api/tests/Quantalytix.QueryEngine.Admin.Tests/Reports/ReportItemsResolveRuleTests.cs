using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_items_resolve</c>: the report dimension's published version must have one root and hold every member
/// line's key and every variable's target member, and a model version being published must keep its variables' targets in
/// it. Sources classify into the version the active assumption set pins; the orchestrator aligns them on its members and
/// evaluates formula lines at its root.
/// </summary>
public sealed class ReportItemsResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded report reads a tree with one root that holds its line and target member.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TreeWithTheNamedMembers_Passes()
    {
        Assert.Empty(await EvaluateAsync(Rule(VersionKinds.ReportVersion), ReportVersionId));
    }

    /// <summary>A second root, a line showing liabilities and a variable targeting equity are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SecondRootAndMissingMembers_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Members.InsertAsync(DimensionRows.Member(3, 501, "memo_items"), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(12, "liabilities", ReportLineKinds.Member, null, 2) with { ReportMemberKey = "liabilities" }, cancellationToken);
        await _fixture.Variables.InsertAsync(Variable(52, "equity", "equity"), cancellationToken);

        var violations = await EvaluateAsync(Rule(VersionKinds.ReportVersion), ReportVersionId);

        Assert.Equal(
            ["Report 'balance_sheet' reads a report dimension whose published version has 2 roots; it needs one.",
             "Report 'balance_sheet' line 'liabilities' shows member 'liabilities', which the published report-item version lacks.",
             "Report 'balance_sheet' variable 'equity' targets member 'equity', which the published report-item version lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A model version whose variable targets a member the report dimension lacks is reported through the report.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ModelVersionTargetingAMissingMember_IsReported()
    {
        await _fixture.Variables.InsertAsync(Variable(53, "balance", "equity") with { ModelVersionId = DraftModelVersionId }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(VersionKinds.CalculationModelVersion), DraftModelVersionId));

        Assert.Equal("Report 'balance_sheet' variable 'balance' targets member 'equity', which the published report-item version lacks.", violation.Message);
    }

    /// <summary>The rule for a kind, over the fixture's tables.</summary>
    private ReportItemsResolveRule Rule(string kind)
    {
        return new ReportItemsResolveRule(kind, _fixture.Reader(), _fixture.PublishedDimensions());
    }
}

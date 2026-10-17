using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseReportFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>report_item_version_consistent</c>: sources that resolve different report-item versions are reported, and so
/// are a report-item version with more than one root, a member line whose member the version lacks and a variable target the
/// version lacks. The orchestrator aligns every source on the nodes of one tree and evaluates formula lines at its root.
/// </summary>
public sealed class ReportItemVersionRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReleaseReportFixture _fixture = new();

    /// <summary>The seeded report reads one tree with one root that holds its line and target member.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SharedTreeWithTheNamedMembers_Passes()
    {
        Assert.Empty(await EvaluateAsync(Rule()));
    }

    /// <summary>A loans source whose binding pins report-item version 502 disagrees with GL's 501.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourcesOnDifferentVersions_AreReported()
    {
        await _fixture.PinLoansAsync(502);
        await _fixture.Sources.InsertAsync(Source(72, 51, "loan_balance", LoansDatasetId), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule()));

        Assert.Equal("Report 'balance_sheet' sources resolve 2 report-item versions; they must share one.", violation.Message);
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

        var violations = await EvaluateAsync(Rule());

        Assert.Equal(
            ["Report 'balance_sheet' reads report-item version 501, which has 2 roots; it needs one.",
             "Report 'balance_sheet' line 'liabilities' shows member 'liabilities', which the report-item version lacks.",
             "Report 'balance_sheet' variable 'equity' targets member 'equity', which the report-item version lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The rule over the fixture's tables.</summary>
    private ReportItemVersionRule Rule()
    {
        return new ReportItemVersionRule(_fixture.Reader(), _fixture.Members);
    }
}

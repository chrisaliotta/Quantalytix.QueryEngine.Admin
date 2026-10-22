using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_styles_resolve</c>. The engine resolves each row's style to its own, or else its role's default, so every style
/// a row names must be one of the report's, and each role can have only one default style.
/// </summary>
public sealed class ReportStylesResolveRuleTests
{
    /// <summary>The seeded tables: report <c>balance_sheet</c> with member rows <c>total</c> and <c>assets</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>Rows that name the report's styles, and one default per role, pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RowsNamingTheReportsStyles_Pass()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Styles.InsertManyAsync([Style("emphasis", null), Style("totals", RowRoles.Total), Style("headers", RowRoles.Header)], cancellationToken);
        await _fixture.MemberOptions.InsertAsync(MemberOption("assets") with { StyleKey = "emphasis" }, cancellationToken);
        await _fixture.Lines.InsertAsync(Line(21, "NOTE", ReportLineKinds.Section, "total", 0) with { StyleKey = "headers" }, cancellationToken);

        Assert.Empty(await _fixture.EvaluateAsync(new ReportStylesResolveRule()));
    }

    /// <summary>A member row and an overlay row naming a style the report lacks, and two defaults of one role, are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MissingStylesAndRepeatedDefaults_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Styles.InsertManyAsync([Style("totals", RowRoles.Total), Style("grand", RowRoles.Total)], cancellationToken);
        await _fixture.MemberOptions.InsertAsync(MemberOption("assets") with { StyleKey = "gone" }, cancellationToken);
        await _fixture.Lines.InsertAsync(Line(21, "NOTE", ReportLineKinds.Section, "total", 0) with { StyleKey = "missing" }, cancellationToken);

        Assert.Equal(
            ["Report 'balance_sheet' row 'NOTE' shows style 'missing', which is no style of the report.",
             "Report 'balance_sheet' row 'assets' shows style 'gone', which is no style of the report.",
             "Report 'balance_sheet' styles 'grand', 'totals' are each the default of total rows; a role has at most one default style."],
            (await _fixture.EvaluateAsync(new ReportStylesResolveRule())).Select(violation => violation.Message));
    }

    /// <summary>A bold style of report 60 that is the default of a role, or of none.</summary>
    private static ReportStyleRecord Style(string styleKey, string? defaultRole)
    {
        return new ReportStyleRecord { ReportId = ReportId, StyleKey = styleKey, DisplayName = styleKey, DefaultRole = defaultRole, FontWeight = FontWeights.Bold, IsItalic = false };
    }
}

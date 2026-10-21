using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the rules that check a report against its calculation model. A report reads its model's metrics by key, so
/// <c>report_metrics_resolve</c> requires every column, formula row, scope, source variable and profile pick to name a metric of
/// the model, and a report runs under its default profile, so <c>default_profile_enabled</c> requires that profile to be enabled.
/// What a write to the model does to its enabled reports, <c>ModelChangesTests</c> confirms.
/// </summary>
public sealed class ReportModelRulesTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded report's metrics resolve in its model, and it defaults to its enabled profile.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ReportOfItsModel_Passes()
    {
        Assert.Empty(await _fixture.EvaluateAsync(new ReportMetricsResolveRule()));
        Assert.Empty(await _fixture.EvaluateAsync(new DefaultProfileEnabledRule()));
    }

    /// <summary>
    /// A column of a calculated measure the model lacks, a formula row of another, and a scope, source variable and pick of a variable
    /// it lacks are each reported.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MetricsTheModelLacks_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Columns.InsertAsync(Column(2, "margin", ReportValueKinds.Calculated, "margin", 2), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(12, "spread", ReportLineKinds.Formula, null, 2) with { CalculatedMeasureKey = "spread", ValueColumnId = 2 }, cancellationToken);
        await _fixture.Scopes.InsertAsync(Scope("income", "assets"), cancellationToken);
        await _fixture.SourceVariables.InsertAsync(SourceVariable(GlSourceId, "income", "income"), cancellationToken);
        await _fixture.Picks.InsertAsync(Pick(75, "income", GlSourceId), cancellationToken);

        var violations = await _fixture.EvaluateAsync(new ReportMetricsResolveRule());

        Assert.Equal(
            ["Report 'balance_sheet' column 'margin' returns 'margin', which is no calculated metric of the model.",
             "Report 'balance_sheet' row 'spread' evaluates 'spread', which is no calculated measure of the model.",
             "Report 'balance_sheet' scopes 'income', which is no variable of the model.",
             "Report 'balance_sheet' source 'gl' supplies 'income', which is no variable of the model.",
             "Report 'balance_sheet' profile 'base' picks a source for 'income', which is no variable of the model."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A report whose default profile is disabled is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DisabledDefaultProfile_IsReported()
    {
        await _fixture.Profiles.InsertAsync(Profile(75, "base") with { IsEnabled = false }, TestContext.Current.CancellationToken);

        var violation = Assert.Single(await _fixture.EvaluateAsync(new DefaultProfileEnabledRule()));

        Assert.Equal("Report 'balance_sheet' defaults to profile 'base', which is no enabled profile of the report.", violation.Message);
    }
}

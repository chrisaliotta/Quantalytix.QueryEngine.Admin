using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the rules that check a report against a version of its calculation model. A report reads its model's published version,
/// so <c>report_metrics_resolve</c> requires every column, formula row, scope, source variable and profile pick to name a metric of
/// it. Publishing a model version makes it the version every enabled report of the model reads, so the rule runs against it for
/// each of those reports: a version that drops a metric a report reads is reported before it is published.
/// </summary>
public sealed class ReportModelRulesTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded report's metrics resolve in the published model version, and it defaults to its enabled profile.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ReportOfThePublishedModel_Passes()
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
            ["Report 'balance_sheet' column 'margin' returns 'margin', which is no calculated metric of the model version.",
             "Report 'balance_sheet' row 'spread' evaluates 'spread', which is no calculated measure of the model version.",
             "Report 'balance_sheet' scopes 'income', which is no variable of the model version.",
             "Report 'balance_sheet' source 'gl' supplies 'income', which is no variable of the model version.",
             "Report 'balance_sheet' profile 'base' picks a source for 'income', which is no variable of the model version."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>
    /// The draft model version 42 lacks the balance variable the report's column, scope, source variable and pick name, so validating
    /// it reports each through the enabled report; the report itself still passes against the published version. A disabled report
    /// is not checked, because the engine does not read it.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ModelVersionDroppingWhatAnEnabledReportReads_IsReported()
    {
        var metrics = await _fixture.EvaluateConsumersAsync(new ReportMetricsResolveRule(), VersionKinds.CalculationModelVersion, DraftModelVersionId);

        Assert.Equal(
            ["Report 'balance_sheet' column 'amount' returns 'balance', which is no variable metric of the model version.",
             "Report 'balance_sheet' scopes 'balance', which is no variable of the model version.",
             "Report 'balance_sheet' source 'gl' supplies 'balance', which is no variable of the model version.",
             "Report 'balance_sheet' profile 'base' picks a source for 'balance', which is no variable of the model version."],
            metrics.Select(violation => violation.Message));
        Assert.Empty(await _fixture.EvaluateAsync(new ReportMetricsResolveRule()));

        var report = await _fixture.Reports.GetAsync(new { ReportId }, TestContext.Current.CancellationToken);
        await _fixture.Reports.UpdateAsync(report with { IsEnabled = false }, TestContext.Current.CancellationToken);
        Assert.Empty(await _fixture.EvaluateConsumersAsync(new ReportMetricsResolveRule(), VersionKinds.CalculationModelVersion, DraftModelVersionId));
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

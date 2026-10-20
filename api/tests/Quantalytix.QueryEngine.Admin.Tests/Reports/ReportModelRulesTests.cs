using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the rules that check a report against a version of its calculation model. A report version reads its model's
/// published version, so <c>model_version_published</c> requires one and <c>report_metrics_resolve</c> requires every column
/// and formula line to name a metric of it. Publishing a model version makes it the version every published report of the
/// model reads, so the same rules run against it for each of those reports: a version that drops a metric a report returns,
/// or disables the profile a report defaults to, is reported before it is published.
/// </summary>
public sealed class ReportModelRulesTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The seeded report's column and profile resolve in the published model version.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ReportOfThePublishedModel_Passes()
    {
        Assert.Empty(await EvaluateAsync(new ReportMetricsResolveRule(VersionKinds.ReportVersion, _fixture.Reader()), ReportVersionId));
        Assert.Empty(await EvaluateAsync(new DefaultProfileEnabledRule(VersionKinds.ReportVersion, _fixture.Reader()), ReportVersionId));
    }

    /// <summary>A column of a calculated measure the model lacks and a formula line of another are each reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MetricsTheModelLacks_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Columns.InsertAsync(Column(2, "margin", ReportValueKinds.Calculated, "margin", 2), cancellationToken);
        await _fixture.Lines.InsertAsync(Line(12, "spread", ReportLineKinds.Formula, null, 2) with { CalculatedMeasureKey = "spread", ValueColumnId = 2 }, cancellationToken);

        var violations = await EvaluateAsync(new ReportMetricsResolveRule(VersionKinds.ReportVersion, _fixture.Reader()), ReportVersionId);

        Assert.Equal(
            ["Report 'balance_sheet' column 'margin' returns 'margin', which is no calculated metric of the model version.",
             "Report 'balance_sheet' line 'spread' evaluates 'spread', which is no calculated measure of the model version."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>
    /// The draft model version 42 has neither the balance variable the report's column returns nor an enabled base profile,
    /// so validating it reports both through the report.
    /// </summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ModelVersionDroppingWhatAPublishedReportReads_IsReported()
    {
        await _fixture.Profiles.InsertAsync(Profile(76, "base") with { ModelVersionId = DraftModelVersionId, IsEnabled = false }, TestContext.Current.CancellationToken);

        var metrics = await EvaluateAsync(new ReportMetricsResolveRule(VersionKinds.CalculationModelVersion, _fixture.Reader()), DraftModelVersionId);
        var profiles = await EvaluateAsync(new DefaultProfileEnabledRule(VersionKinds.CalculationModelVersion, _fixture.Reader()), DraftModelVersionId);

        Assert.Equal("Report 'balance_sheet' column 'amount' returns 'balance', which is no variable metric of the model version.", Assert.Single(metrics).Message);
        Assert.Equal("Report 'balance_sheet' defaults to profile 'base', which is no enabled profile of the model version.", Assert.Single(profiles).Message);
    }

    /// <summary>A report whose model has only a draft version is reported, naming the model.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ModelWithoutPublishedVersion_IsReported()
    {
        var modelVersions = new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion(DraftModelVersionId, PublicationStatuses.Draft));
        var rule = new ModelVersionPublishedRule(
            _fixture.ReportVersions,
            new InMemoryTableGateway<CalculationModelRecord>(new CalculationModelRecord { ModelId = ModelId, ModelKey = "margin_model", DisplayName = "Margin", IsEnabled = true }),
            new ModelMetricsReader(_fixture.ReportVersions, modelVersions, _fixture.Variables, _fixture.CalculatedMeasures));

        var violation = Assert.Single(await EvaluateAsync(rule, ReportVersionId));

        Assert.Equal(ModelVersionPublishedRule.Code, violation.Rule);
        Assert.Equal("Calculation model 'margin_model' has no published version; publish one first.", violation.Message);
    }
}

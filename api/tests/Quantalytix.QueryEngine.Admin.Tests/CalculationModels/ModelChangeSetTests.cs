using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the model change set the model editor saves through (blueprint 15, OI-42). A model has no draft: the engine reads it as
/// it stands through each enabled report over it, so every edit is applied in memory and refused with 422, writing nothing, when it
/// introduces a model-rule violation or a report-rule violation of a report over the model; a violation the stored model already has
/// does not refuse, but is listed. Keys are fixed and name the items an operation edits. A commit re-runs the gate, writes the rows,
/// and advances the semantic revision only when an enabled report reads the model.
/// </summary>
public sealed class ModelChangeSetTests
{
    /// <summary>The seeded model.</summary>
    private const long ModelId = ModelRows.CalculationModelId;

    /// <summary>The tables and the module.</summary>
    private readonly ModelFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A preview shows the pending document with the new metrics and their default drivers, and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_ShowsThePendingDocumentAndWritesNothing()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(
            ModelId, Request(new ModelSaveVariableOperation { Input = Variable("income") }, new ModelSaveMeasureOperation { Input = Measure("margin", "[m.income] / [m.balance]") }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["balance", "income"], outcome.Value.State.Variables.Select(variable => variable.Key));
        Assert.Equal(["income", "balance"], outcome.Value.State.EffectiveDrivers["margin"].Select(driver => driver.Key));
        Assert.Equal(["balance"], _fixture.Tables.Variables.All.Select(variable => variable.VariableKey));
    }

    /// <summary>A commit writes the rows with new ids and advances the revision, because an enabled report reads the model.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesTheRowsAndAdvancesTheRevision()
    {
        var revision = _fixture.Revision;

        var outcome = await _fixture.Pipeline().CommitAsync(ModelId, Request(new ModelSaveVariableOperation { Input = Variable("income") }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(FirstNewId, _fixture.Tables.Variables.All.Single(variable => variable.VariableKey == "income").VariableId);
        Assert.Equal(revision + 1, _fixture.Revision);
    }

    /// <summary>A model no enabled report reads is not read by the engine, so its commit keeps the revision.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task CommitOfAModelNoEnabledReportReads_KeepsTheRevision()
    {
        await _fixture.DisableReportAsync();
        var revision = _fixture.Revision;

        var outcome = await _fixture.Pipeline().CommitAsync(ModelId, Request(new ModelUpdateModelOperation { Patch = new CalcModelPatch { DisplayName = "Renamed" } }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal("Renamed", _fixture.Tables.Models.All.Single().DisplayName);
        Assert.Equal(revision, _fixture.Revision);
    }

    /// <summary>A formula that reads a key the model lacks, or reads itself, is refused by the rule it breaks, and nothing is written.</summary>
    /// <param name="formula">Yield's new formula.</param>
    /// <param name="rule">The rule that refuses it.</param>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Theory]
    [InlineData("[m.income] / 2", FormulaReferencesResolveRule.Code)]
    [InlineData("[m.yield] / 2", FormulasAcyclicRule.Code)]
    public async Task EditIntroducingAModelRuleViolation_IsRefused(string formula, string rule)
    {
        var outcome = await _fixture.Pipeline().CommitAsync(ModelId, Request(new ModelSaveMeasureOperation { OriginalKey = "yield", Input = Measure("yield", formula) }), Token);

        Assert.Equal(RefusalKind.RulesViolated, outcome.Refusal!.Kind);
        Assert.Equal([rule], outcome.Refusal.Violations.Select(violation => violation.Code));
        Assert.Equal("[m.balance] / 2", _fixture.Tables.CalculatedMeasures.All.Single().ExpressionText);
    }

    /// <summary>
    /// Once yield is gone, the enabled report still reads balance, so deleting balance is refused by the report's
    /// <c>report_metrics_resolve</c>; with the report disabled the engine no longer reads it, and the same delete applies.
    /// </summary>
    /// <param name="reportEnabled">Whether the report is enabled.</param>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeletingAVariableAnEnabledReportReads_IsRefused(bool reportEnabled)
    {
        if (!reportEnabled) await _fixture.DisableReportAsync();

        var outcome = await _fixture.Pipeline().PreviewAsync(
            ModelId, Request(new ModelDeleteMeasureOperation { Key = "yield" }, new ModelDeleteVariableOperation { Key = "balance" }), Token);

        if (reportEnabled) Assert.Equal([ReportMetricsResolveRule.Code], outcome.Refusal!.Violations.Select(violation => violation.Code).Distinct());
        else Assert.True(outcome.Succeeded);
    }

    /// <summary>
    /// Narrowing a parameter's allowed values below a value a report row sets is refused by <c>report_row_parameters_resolve</c>, for
    /// every report over the model, enabled or not, because enabling a report does not re-check the model's parameters.
    /// </summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task NarrowingAParameterARowSets_IsRefused()
    {
        await _fixture.Tables.Parameters.InsertAsync(ModelRows.ParameterRow(ModelRows.DayCount()), Token);
        await _fixture.Tables.RowParameters.InsertAsync(
            new ReportRowParameterRecord { ReportId = ReportRows.ReportId, RowKey = "assets", ParameterKey = "dayCount", TextValue = DayCountConventions.Actual360 }, Token);
        await _fixture.DisableReportAsync();

        var outcome = await _fixture.Pipeline().PreviewAsync(
            ModelId, Request(new ModelSaveParameterOperation { OriginalKey = "dayCount", Input = DayCount(DayCountConventions.ActualActual) }), Token);

        Assert.Equal([ReportRowParametersResolveRule.Code], outcome.Refusal!.Violations.Select(violation => violation.Code));
    }

    /// <summary>Declaring a parameter an enabled report's default filter compares is refused by <c>report_defaults_resolve</c>.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeclaringAParameterADefaultFilterCompares_IsRefused()
    {
        await _fixture.Tables.Reports.UpdateAsync(
            _fixture.Tables.Reports.All.Single() with { DefaultFilterJson = ReportDefaultsResolveRuleTests.FilterRegionBy("dayCount") }, Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(ModelId, Request(new ModelSaveParameterOperation { Input = DayCount(DayCountConventions.ActualActual) }), Token);

        Assert.Equal([ReportDefaultsResolveRule.Code], outcome.Refusal!.Violations.Select(violation => violation.Code));
    }

    /// <summary>
    /// A violation the stored model already has refuses no edit, so the model can be repaired step by step, and the preview lists it;
    /// an edit that adds another violation is still refused, with only the new one.
    /// </summary>
    /// <returns>A task that completes when the previews are checked.</returns>
    [Fact]
    public async Task PreExistingViolations_DoNotRefuseButNewOnesDo()
    {
        await _fixture.Tables.CalculatedMeasures.InsertAsync(ModelRows.Calculated(62, "broken", "[m.ghost] * 2"), Token);
        var rename = new ModelUpdateModelOperation { Patch = new CalcModelPatch { DisplayName = "Renamed" } };

        var accepted = await _fixture.Pipeline().PreviewAsync(ModelId, Request(rename), Token);
        var refused = await _fixture.Pipeline().PreviewAsync(
            ModelId, Request(rename, new ModelSaveMeasureOperation { OriginalKey = "yield", Input = Measure("yield", "[m.missing]") }), Token);

        Assert.Equal([FormulaReferencesResolveRule.Code], accepted.Value!.Violations.Select(violation => violation.Code));
        Assert.Equal(
            ["Calculated measure 'yield' reads [m.missing], which is no variable or calculated measure of the model."],
            refused.Refusal!.Violations.Select(violation => violation.Message));
    }

    /// <summary>Keys are fixed once saved: an edit that changes its item's key is refused on the input's key, as a 400.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task ChangingAKey_IsRefused()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(ModelId, Request(new ModelSaveVariableOperation { OriginalKey = "balance", Input = Variable("assets") }), Token);

        Assert.Equal(RefusalKind.Invalid, outcome.Refusal!.Kind);
        Assert.Equal(["operations[0].input.key"], outcome.Refusal.Errors.Keys);
    }

    /// <summary>Variables and calculated measures share one key namespace (C1): a new measure cannot take a variable's key.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task ANewMetricWithATakenKey_IsRefused()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(ModelId, Request(new ModelSaveMeasureOperation { Input = Measure("balance", "1") }), Token);

        Assert.Equal(["The model already has a variable or calculated measure with key 'balance'."], outcome.Refusal!.Errors["operations[0].input.key"]);
    }

    /// <summary>An operation that names an item the model lacks is refused on the key it names, because the contract's change set has no 409.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeletingAMissingItem_IsRefusedOnItsKey()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(ModelId, Request(new ModelDeleteParameterOperation { Key = "rate" }), Token);

        Assert.Equal(["The model has no parameter 'rate'."], outcome.Refusal!.Errors["operations[0].key"]);
    }

    /// <summary>An attribution is written with its drivers in sort order, and deleting its metric deletes both.</summary>
    /// <returns>A task that completes when the commits are checked.</returns>
    [Fact]
    public async Task DeletingAMetric_DeletesItsAttribution()
    {
        var attribution = ModelRows.Attribution("yield", null, ModelRows.Driver("volume", "balance"));
        await _fixture.Pipeline().CommitAsync(ModelId, Request(new ModelSetAttributionOperation { Input = attribution }), Token);
        var stored = _fixture.Tables.AttributionDrivers.All.Single();

        var outcome = await _fixture.Pipeline().CommitAsync(ModelId, Request(new ModelDeleteMeasureOperation { Key = "yield" }), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(("volume", 1), (stored.DriverKey, stored.SortOrder));
        Assert.Empty(_fixture.Tables.Attributions.All);
        Assert.Empty(_fixture.Tables.AttributionDrivers.All);
    }
}

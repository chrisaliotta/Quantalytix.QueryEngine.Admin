using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the gate every write to a model, its metrics or their attributions goes through. A model has no draft: the engine reads
/// it as it stands through each enabled report over it, so a write is applied in memory first and refused, with nothing written,
/// when it would leave a formula reading a key the model lacks, reading itself or calling a function its language lacks, an
/// attribution that no longer resolves or splits its expression, or an enabled report naming a metric the model lacks or reading a
/// variable a profile picks no source for. Disabled reports are not checked, because the engine does not read them; enabling one
/// runs the report rules then.
/// </summary>
public sealed class ModelChangesTests
{
    /// <summary>The seeded tables: enabled report <c>balance_sheet</c> over model 40, whose <c>yield</c> reads <c>[m.balance] / 2</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>The variables, as the variables controller composes and writes them.</summary>
    private readonly VariableService _variables;

    /// <summary>The calculated measures, as the calculated measures controller composes and writes them.</summary>
    private readonly CalculatedMeasureService _measures;

    /// <summary>Builds the content services over the fixture's tables.</summary>
    public ModelChangesTests()
    {
        var ids = new SequentialIdGenerator(1000);
        _variables = new VariableService(_fixture.Variables, ids);
        _measures = new CalculatedMeasureService(_fixture.CalculatedMeasures, ids);
    }

    /// <summary>A formula that reads a key the model lacks, or reads itself, is refused by the rule it breaks, and yield keeps its formula.</summary>
    /// <param name="expressionText">Yield's new formula.</param>
    /// <param name="rule">The rule that refuses it.</param>
    /// <returns>A task that completes when the write is checked.</returns>
    [Theory]
    [InlineData("[m.income] / 2", FormulaReferencesResolveRule.Code)]
    [InlineData("[m.yield] / 2", FormulasAcyclicRule.Code)]
    public async Task BrokenFormula_IsRefusedAndNothingIsWritten(string expressionText, string rule)
    {
        var violations = await UpdateMeasureAsync(Calculated(61, "yield", expressionText));

        Assert.Equal([rule], violations.Select(violation => violation.Rule));
        Assert.Equal("[m.balance] / 2", Assert.Single(_fixture.CalculatedMeasures.All).ExpressionText);
    }

    /// <summary>
    /// Once yield is gone, only the enabled report reads balance, through its column, scope, source and pick; deleting balance is
    /// refused by <c>report_metrics_resolve</c>, because the engine would run the report against a variable that no longer exists.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingAVariableAnEnabledReportReads_IsRefused()
    {
        await _fixture.CalculatedMeasures.DeleteAsync(new { CalculatedMeasureId = 61 }, TestContext.Current.CancellationToken);

        var violations = await DeleteVariableAsync(51);

        Assert.Equal([ReportMetricsResolveRule.Code], violations.Select(violation => violation.Rule).Distinct());
        Assert.Single(_fixture.Variables.All);
    }

    /// <summary>The same delete runs once the report is disabled, because the engine no longer reads the report.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingAVariableOnlyADisabledReportReads_IsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.CalculatedMeasures.DeleteAsync(new { CalculatedMeasureId = 61 }, cancellationToken);
        await _fixture.Reports.UpdateAsync(Assert.Single(_fixture.Reports.All) with { IsEnabled = false }, cancellationToken);

        var violations = await DeleteVariableAsync(51);

        Assert.Empty(violations);
        Assert.Empty(_fixture.Variables.All);
    }

    /// <summary>
    /// With a yield column, making yield read income makes the report read income, which profile <c>base</c> picks no source for:
    /// <c>profiles_complete</c> refuses the edit, because a run under the profile would have no data for income.
    /// </summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task FormulaThatLeavesAProfileWithoutAPick_IsRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Variables.InsertAsync(Variable(52, "income"), cancellationToken);
        await _fixture.Columns.InsertAsync(Column(2, "yield", ReportValueKinds.Calculated, "yield", 2), cancellationToken);

        var violations = await UpdateMeasureAsync(Calculated(61, "yield", "[m.income] / [m.balance]"));

        Assert.Equal(["Report 'balance_sheet' profile 'base' picks no source for variable 'income'."], violations.Select(violation => violation.Message));
        Assert.Equal("[m.balance] / 2", Assert.Single(_fixture.CalculatedMeasures.All).ExpressionText);
    }

    /// <summary>An edit that keeps every formula and the enabled report resolving is written as it stands, with no draft to publish.</summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task ChangeThatKeepsEverythingResolving_IsWritten()
    {
        var violations = await UpdateMeasureAsync(Calculated(61, "yield", "[m.balance] / 4"));

        Assert.Empty(violations);
        Assert.Equal("[m.balance] / 4", Assert.Single(_fixture.CalculatedMeasures.All).ExpressionText);
    }

    /// <summary>
    /// An attribution is a live model write: one whose driver switches an input its expression does not read is refused and nothing is
    /// stored, and one that passes is stored with its drivers, which list back as written.
    /// </summary>
    /// <returns>A task that completes when both writes are checked.</returns>
    [Fact]
    public async Task Attribution_IsWrittenOnlyWhenItPassesTheModelRules()
    {
        var refused = await ReplaceAttributionAsync(Attribution("yield", null, Driver("volume", 1, "balance"), Driver("income", 2, "income")));
        var written = await ReplaceAttributionAsync(Attribution("yield", null, Driver("volume", 1, "balance")) with { WeightVariableKey = "balance" });

        Assert.Equal([AttributionReferencesResolveRule.Code, AttributionDriversRule.Code], refused.Select(violation => violation.Rule));
        Assert.Empty(written);
        var stored = Assert.Single(await _fixture.AttributionService().ListAsync(ModelId, TestContext.Current.CancellationToken));
        Assert.Equal(("yield", "balance"), (stored.MetricKey, stored.WeightVariableKey));
        Assert.Equal(["balance"], Assert.Single(stored.Drivers).InputKeys);
    }

    /// <summary>
    /// The attribution rules run on every metric write too: deleting yield, which an attribution explains, is refused, because the
    /// engine would read an attribution of a metric the model no longer has.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingAnAttributedMeasure_IsRefused()
    {
        Assert.Empty(await ReplaceAttributionAsync(Attribution("yield", null)));

        var violations = await _fixture.Changes().ApplyAsync(
            ModelId, model => _measures.Without(model, 61), () => _measures.DeleteAsync(61, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.Equal(["The attribution of 'yield' names no variable or calculated measure of the model."], violations.Select(violation => violation.Message));
        Assert.Single(_fixture.CalculatedMeasures.All);
    }

    /// <summary>
    /// A model on formula language 1.1 whose yield calls POWER cannot go back to 1.0, because the engine would then refuse the formula;
    /// the model keeps 1.1.
    /// </summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task LeavingTheLanguageAFormulaNeeds_IsRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Models.UpdateAsync(Model() with { FormulaLanguageVersion = FormulaLanguageVersions.Version11 }, cancellationToken);
        Assert.Empty(await UpdateMeasureAsync(Calculated(61, "yield", "POWER([m.balance], 2)")));
        var downgraded = Model() with { FormulaLanguageVersion = FormulaLanguageVersions.Version10 };

        var violations = await _fixture.Changes().ApplyAsync(
            ModelId, model => model with { Model = downgraded }, () => _fixture.Models.UpdateAsync(downgraded, cancellationToken), cancellationToken);

        Assert.Equal([FormulasFitLanguageRule.Code], violations.Select(violation => violation.Rule));
        Assert.Equal(FormulaLanguageVersions.Version11, Assert.Single(_fixture.Models.All).FormulaLanguageVersion);
    }

    /// <summary>Replaces an attribution of model 40 through the gate, as the attributions controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> ReplaceAttributionAsync(MetricAttribution attribution)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            ModelId,
            model => MetricAttributionService.Put(model, attribution),
            () => _fixture.AttributionService().ReplaceAsync(ModelId, attribution, cancellationToken),
            cancellationToken);
    }

    /// <summary>Replaces a calculated measure of model 40 through the gate, as the calculated measures controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> UpdateMeasureAsync(CalculatedMeasureRecord updated)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            ModelId, model => _measures.Put(model, updated), () => _measures.UpdateAsync(updated, cancellationToken), cancellationToken);
    }

    /// <summary>Deletes a variable of model 40 through the gate, as the variables controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> DeleteVariableAsync(long variableId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            ModelId, model => _variables.Without(model, variableId), () => _variables.DeleteAsync(variableId, cancellationToken), cancellationToken);
    }
}

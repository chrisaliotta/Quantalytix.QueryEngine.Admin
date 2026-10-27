using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms that parameter writes are live model writes. The engine reads a parameter through the formulas that read it and the
/// report rows that set it, so a delete, retype or change of allowed values that would leave a formula reading it out of type, or any
/// report's row value, enabled or not, outside its parameter, is refused and nothing is written. Model 40 is on formula language 1.2
/// with the seed's <c>dayCount</c>; report <c>balance_sheet</c> over it has rows <c>total</c> and <c>assets</c>.
/// </summary>
public sealed class ModelParameterChangesTests
{
    /// <summary>The seeded tables: report <c>balance_sheet</c> over model 40, whose <c>yield</c> reads <c>[m.balance] / 2</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>A formula reads the parameter, so deleting it is refused, and the parameter stays.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingAParameterAFormulaReads_IsRefused()
    {
        await UseDayCountAsync(yieldReadsIt: true, assetsSets: null);

        var violations = await DeleteAsync("dayCount");

        Assert.Equal(
            [(FormulaParametersResolveRule.Code, "Calculated measure 'yield' reads @dayCount, which is no parameter of the model, @periodDays or @yearDays.")],
            violations.Select(violation => (violation.Code, violation.Message)));
        Assert.Single(_fixture.Parameters.All);
    }

    /// <summary>
    /// A row of a disabled report sets the parameter, so deleting it is refused too: the report would carry a value of a parameter its
    /// model lacks, and enabling it would fail.
    /// </summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeletingAParameterADisabledReportsRowSets_IsRefused()
    {
        await UseDayCountAsync(yieldReadsIt: false, assetsSets: DayCountConventions.Actual360);
        await _fixture.Reports.UpdateAsync(Report() with { DefaultProfileKey = "base", IsEnabled = false }, TestContext.Current.CancellationToken);

        var violations = await DeleteAsync("dayCount");

        Assert.Equal(
            [(ReportRowParametersResolveRule.Code, "Report 'balance_sheet' row 'assets' sets 'dayCount', which is no parameter of the report's model.")],
            violations.Select(violation => (violation.Code, violation.Message)));
        Assert.Single(_fixture.Parameters.All);
    }

    /// <summary>
    /// Retyping the day count to a number breaks YEARFRAC's argument and the row's text value; both are reported, and the stored
    /// parameter keeps its text type.
    /// </summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task RetypingAParameterThatIsReadAsText_IsRefused()
    {
        await UseDayCountAsync(yieldReadsIt: true, assetsSets: DayCountConventions.Actual360);

        var violations = await PutAsync(NumberParameter("dayCount", 1m));

        Assert.Equal(
            [(FormulaParametersResolveRule.Code, "Calculated measure 'yield' calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as 'actual_360', or from a text parameter whose allowed values are conventions."),
             (ReportRowParametersResolveRule.Code, "Report 'balance_sheet' row 'assets' sets parameter 'dayCount', which is a number parameter, so its value must be a number.")],
            violations.Select(violation => (violation.Code, violation.Message)));
        Assert.Equal(ParameterValueTypes.Text, Assert.Single(_fixture.Parameters.All).ValueType);
    }

    /// <summary>
    /// Allowed values that leave out a value a row sets, or that admit text YEARFRAC cannot read, are refused by the rule they break.
    /// </summary>
    /// <param name="allowedValues">The day count's new allowed values.</param>
    /// <param name="rule">The rule that refuses them.</param>
    /// <returns>A task that completes when the write is checked.</returns>
    [Theory]
    [InlineData(new[] { DayCountConventions.ActualActual }, ReportRowParametersResolveRule.Code)]
    [InlineData(new[] { DayCountConventions.ActualActual, DayCountConventions.Actual360, "custom" }, FormulaParametersResolveRule.Code)]
    public async Task AllowedValuesThatBreakARowOrYearFrac_AreRefused(string[] allowedValues, string rule)
    {
        await UseDayCountAsync(yieldReadsIt: true, assetsSets: DayCountConventions.Actual360);

        var violations = await PutAsync(DayCount() with { AllowedValues = allowedValues });

        Assert.Equal([rule], violations.Select(violation => violation.Code));
        Assert.Equal(DayCount().AllowedValues, Assert.Single(await _fixture.ParameterService().ListAsync(CalculationModelId, TestContext.Current.CancellationToken)).AllowedValues);
    }

    /// <summary>
    /// The enabled report filters by default on @scale, which every request supplies, so declaring a model parameter of that name is
    /// refused: the engine refuses a request that supplies a model parameter, so no request could run the report. Nothing is written.
    /// </summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task DeclaringAParameterADefaultFilterCompares_IsRefused()
    {
        await _fixture.Reports.UpdateAsync(
            Report() with { DefaultProfileKey = "base", DefaultFilterJson = ReportDefaultsResolveRuleTests.FilterRegionBy("scale") },
            TestContext.Current.CancellationToken);

        var violations = await PutAsync(NumberParameter("scale", 1000m));

        Assert.Equal([ReportDefaultsResolveRule.Code], violations.Select(violation => violation.Code));
        Assert.Empty(_fixture.Parameters.All);
    }

    /// <summary>A parameter nothing reads or sets is written, retyped and deleted freely, and lists back as written.</summary>
    /// <returns>A task that completes when the writes are checked.</returns>
    [Fact]
    public async Task UnusedParameter_IsWrittenRetypedAndDeleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await UseDayCountAsync(yieldReadsIt: false, assetsSets: null);

        Assert.Empty(await PutAsync(NumberParameter("scale", 1000m)));
        Assert.Equal(1000m, (await _fixture.ParameterService().ListAsync(CalculationModelId, cancellationToken)).Single(parameter => parameter.ParameterKey == "scale").DefaultNumber);
        Assert.Empty(await PutAsync(TextParameter("scale", "thousands") with { AllowedValues = ["units", "thousands"] }));
        Assert.Equal(["units", "thousands"], (await _fixture.ParameterService().ListAsync(CalculationModelId, cancellationToken)).Single(parameter => parameter.ParameterKey == "scale").AllowedValues);
        Assert.Empty(await DeleteAsync("scale"));
        Assert.Equal(["dayCount"], (await _fixture.ParameterService().ListAsync(CalculationModelId, cancellationToken)).Select(parameter => parameter.ParameterKey));
    }

    /// <summary>
    /// A model whose yield calls YEARFRAC with a declared parameter cannot go back to formula language 1.1, because the engine would
    /// then refuse the formula; the model keeps 1.2.
    /// </summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task LeavingLanguage12WhileAFormulaUsesIt_IsRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await UseDayCountAsync(yieldReadsIt: true, assetsSets: null);
        var downgraded = Model() with { FormulaLanguageVersion = FormulaLanguageVersions.Version11 };

        var violations = await _fixture.Changes().ApplyAsync(
            CalculationModelId, model => model with { Model = downgraded }, () => _fixture.Models.UpdateAsync(downgraded, cancellationToken), cancellationToken);

        Assert.Equal([FormulasFitLanguageRule.Code, FormulasFitLanguageRule.Code], violations.Select(violation => violation.Code));
        Assert.Equal(FormulaLanguageVersions.Version12, Assert.Single(_fixture.Models.All).FormulaLanguageVersion);
    }

    /// <summary>
    /// Moves model 40 to formula language 1.2 with the seed's day count parameter, optionally makes yield annualize by it, and
    /// optionally has row <c>assets</c> set it.
    /// </summary>
    private async Task UseDayCountAsync(bool yieldReadsIt, string? assetsSets)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Models.UpdateAsync(Model() with { FormulaLanguageVersion = FormulaLanguageVersions.Version12 }, cancellationToken);
        await _fixture.ParameterService().ReplaceAsync(CalculationModelId, DayCount(), cancellationToken);
        if (yieldReadsIt) await _fixture.CalculatedMeasures.UpdateAsync(Calculated(61, "yield", "[m.balance] / YEARFRAC(@dayCount)"), cancellationToken);
        if (assetsSets is not null)
        {
            await _fixture.RowParameters.InsertAsync(
                new ReportRowParameterRecord { ReportId = ReportId, RowKey = "assets", ParameterKey = "dayCount", TextValue = assetsSets }, cancellationToken);
        }
    }

    /// <summary>Writes a parameter of model 40 through the gate, as the parameters controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> PutAsync(ModelParameter parameter)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            CalculationModelId, model => ModelParameterService.Put(model, parameter), () => _fixture.ParameterService().ReplaceAsync(CalculationModelId, parameter, cancellationToken), cancellationToken);
    }

    /// <summary>Deletes a parameter of model 40 through the gate, as the parameters controller does.</summary>
    private Task<IReadOnlyList<RuleViolation>> DeleteAsync(string parameterKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        return _fixture.Changes().ApplyAsync(
            CalculationModelId, model => ModelParameterService.Without(model, parameterKey), () => _fixture.ParameterService().DeleteAsync(CalculationModelId, parameterKey, cancellationToken), cancellationToken);
    }
}

using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the shape of an attribution when it is written: its expression is a model formula, over model keys and the context
/// parameters only, because the engine evaluates it over aligned report values; each driver has a key the engine can name a column
/// by, other than <c>residual</c>, which names the unexplained part, and lists its inputs once.
/// </summary>
public sealed class MetricAttributionInputValidatorTests
{
    /// <summary>The validator.</summary>
    private readonly MetricAttributionInputValidator _validator = new();

    /// <summary>The seed's attribution of interest income, and an attribution that keeps the formula and the default drivers, pass.</summary>
    [Fact]
    public void WellFormedAttributions_Pass()
    {
        var identity = new MetricAttributionInput
        {
            ExpressionText = "[m.AVERAGE_BALANCE] * [m.YIELD] * @periodDays / @yearDays",
            Drivers = [Driver("volume", 1, "AVERAGE_BALANCE"), Driver("rate", 2, "YIELD"), Driver("days", 3, "@periodDays", "@yearDays")]
        };

        _validator.TestValidate(identity).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new MetricAttributionInput { WeightVariableKey = "AVERAGE_BALANCE", Drivers = [] }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>An expression that reads a dataset field, a driver keyed residual and a driver that lists an input twice each fail on their path.</summary>
    [Fact]
    public void MalformedAttribution_FailsOnItsPaths()
    {
        var input = new MetricAttributionInput
        {
            ExpressionText = "[f.balance] * [m.YIELD]",
            Drivers = [Driver("residual", 1, "YIELD"), Driver("rate", 2, "YIELD", "YIELD")]
        };

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(attribution => attribution.ExpressionText);
        result.ShouldHaveValidationErrorFor("Drivers[0].DriverKey");
        result.ShouldHaveValidationErrorFor("Drivers[1].InputKeys");
        Assert.Equal(3, result.Errors.Count);
    }
}

using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the fields of an attribution a <c>setAttribution</c> operation carries: an optional expression that reads model keys and
/// parameters only, because the engine evaluates it over aligned report values; each driver has a key the engine can name a column
/// by, other than <c>residual</c>, which names the unexplained part, and lists its inputs once.
/// </summary>
public sealed class AttributionValidatorTests
{
    /// <summary>The validator.</summary>
    private readonly AttributionValidator _validator = new();

    /// <summary>The seed's attribution of interest income, and an attribution that keeps the formula and the default drivers, pass.</summary>
    [Fact]
    public void WellFormedAttributions_Pass()
    {
        var identity = Attribution(
            "INTEREST_INCOME", "[m.AVERAGE_BALANCE] * [m.YIELD] * @periodDays / @yearDays",
            Driver("volume", "AVERAGE_BALANCE"), Driver("rate", "YIELD"), Driver("days", "@periodDays", "@yearDays"));

        _validator.TestValidate(identity).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(Attribution("YIELD", null) with { WeightVariableKey = "AVERAGE_BALANCE" }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// An expression that reads parameters passes, declared or not: a parameter can be declared after a formula reads it, so whether
    /// each one resolves, and is read where its type belongs, is the model gate's <c>formula_parameters_resolve</c>.
    /// </summary>
    [Fact]
    public void ParameterNames_PassAtInput()
    {
        _validator.TestValidate(Attribution("INTEREST_INCOME", "[m.AVERAGE_BALANCE] * [m.YIELD] * YEARFRAC(@dayCount) * @notDeclaredYet"))
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>An expression that reads a dataset field, a driver keyed residual and a driver that lists an input twice each fail on their path.</summary>
    [Fact]
    public void MalformedAttribution_FailsOnItsPaths()
    {
        var result = _validator.TestValidate(Attribution("YIELD", "[f.balance] * [m.YIELD]", Driver("residual", "YIELD"), Driver("rate", "YIELD", "YIELD")));

        result.ShouldHaveValidationErrorFor(attribution => attribution.Expression);
        result.ShouldHaveValidationErrorFor("Drivers[0].Key");
        result.ShouldHaveValidationErrorFor("Drivers[1].Inputs");
        Assert.Equal(3, result.Errors.Count);
    }
}

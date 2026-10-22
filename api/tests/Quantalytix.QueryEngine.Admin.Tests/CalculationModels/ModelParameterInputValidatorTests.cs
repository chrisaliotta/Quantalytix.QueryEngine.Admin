using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the fields of a model parameter when it is written. Its default is the value of every row no row above sets, so a number
/// parameter has a number default and nothing else, and a text parameter a text default, among its allowed values when it has them;
/// the allowed values are listed once each and fit the row value column. Its key is a stable key other than the two context
/// parameters, which every formula reads for the period.
/// </summary>
public sealed class ModelParameterInputValidatorTests
{
    /// <summary>The input validator.</summary>
    private readonly ModelParameterInputValidator _validator = new();

    /// <summary>The key validator.</summary>
    private readonly ModelParameterReferenceValidator _keys = new();

    /// <summary>A number parameter with a number default, and the seed's day count parameter, pass.</summary>
    [Fact]
    public void WellFormedParameters_Pass()
    {
        _validator.TestValidate(Number()).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(DayCount()).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(DayCount() with { AllowedValues = null, DefaultText = "any text" }).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A number parameter without a number default, or with allowed values or a text default, fails on each field.</summary>
    [Fact]
    public void NumberParameter_HasANumberDefaultOnly()
    {
        var result = _validator.TestValidate(Number() with { DefaultNumber = null, AllowedValues = ["1"], DefaultText = "1" });

        result.ShouldHaveValidationErrorFor(input => input.DefaultNumber);
        result.ShouldHaveValidationErrorFor(input => input.AllowedValues);
        result.ShouldHaveValidationErrorFor(input => input.DefaultText);
        Assert.Equal(3, result.Errors.Count);
    }

    /// <summary>A number default that does not fit DECIMAL(38, 12) fails.</summary>
    [Fact]
    public void NumberDefault_FitsItsColumn()
    {
        _validator.TestValidate(Number() with { DefaultNumber = 0.1234567890123m }).ShouldHaveValidationErrorFor(input => input.DefaultNumber);
    }

    /// <summary>A text parameter without a text default, or with a number default, fails on each field.</summary>
    [Fact]
    public void TextParameter_HasATextDefaultOnly()
    {
        var result = _validator.TestValidate(DayCount() with { DefaultText = null, DefaultNumber = 1m });

        result.ShouldHaveValidationErrorFor(input => input.DefaultText);
        result.ShouldHaveValidationErrorFor(input => input.DefaultNumber);
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>
    /// Allowed values are a non-empty list of non-empty texts, each once and within 256 bytes, and the default is one of them, because
    /// every row without a value of its own takes the default.
    /// </summary>
    [Fact]
    public void AllowedValues_AreDistinctTextsThatHoldTheDefault()
    {
        _validator.TestValidate(DayCount() with { AllowedValues = [] }).ShouldHaveValidationErrorFor(input => input.AllowedValues);
        _validator.TestValidate(DayCount() with { AllowedValues = ["actual_actual", "actual_actual"] })
            .ShouldHaveValidationErrorFor(input => input.AllowedValues)
            .WithErrorMessage("'Allowed Values' must list each value once.");
        _validator.TestValidate(DayCount() with { AllowedValues = ["actual_actual", ""] }).ShouldHaveValidationErrorFor("AllowedValues[1]");
        _validator.TestValidate(DayCount() with { AllowedValues = ["actual_actual", new string('x', 257)] }).ShouldHaveValidationErrorFor("AllowedValues[1]");
        _validator.TestValidate(DayCount() with { DefaultText = "actual_360", AllowedValues = ["actual_actual"] })
            .ShouldHaveValidationErrorFor(input => input.DefaultText)
            .WithErrorMessage("'Default Text' must be one of the allowed values.");
    }

    /// <summary>A type outside the vocabulary fails, and no type-specific rule runs for it.</summary>
    [Fact]
    public void UnknownType_FailsAlone()
    {
        var result = _validator.TestValidate(Number() with { ValueType = "date" });

        result.ShouldHaveValidationErrorFor(input => input.ValueType);
        Assert.Single(result.Errors);
    }

    /// <summary>A stable key passes; a malformed key, and either context parameter's name, fail.</summary>
    /// <param name="parameterKey">The key the route names.</param>
    /// <param name="passes">Whether it passes.</param>
    [Theory]
    [InlineData("dayCount", true)]
    [InlineData("1rate", false)]
    [InlineData("day count", false)]
    [InlineData("periodDays", false)]
    [InlineData("yearDays", false)]
    public void Keys_AreStableAndNotContextParameters(string parameterKey, bool passes)
    {
        var result = _keys.TestValidate(new ModelParameterReference { ModelId = ModelRows.ModelId, ParameterKey = parameterKey });

        if (passes) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(reference => reference.ParameterKey);
    }

    /// <summary>A number parameter defaulting to 0.05.</summary>
    private static ModelParameterInput Number()
    {
        return new ModelParameterInput { DisplayName = "Rate", ValueType = ParameterValueTypes.Number, DefaultNumber = 0.05m };
    }

    /// <summary>The seed's day count parameter.</summary>
    private static ModelParameterInput DayCount()
    {
        return new ModelParameterInput
        {
            DisplayName = "Day count",
            ValueType = ParameterValueTypes.Text,
            AllowedValues = [.. DayCountConventions.Definition.Values.Select(value => value.Value)],
            DefaultText = DayCountConventions.ActualActual
        };
    }
}

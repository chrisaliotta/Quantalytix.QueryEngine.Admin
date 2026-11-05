using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the output type of a calculated measure a <c>saveMeasure</c> operation carries. The engine evaluates a report formula in
/// double when its declared output is double, so a calculated measure may declare a double output, which like every non-decimal
/// output carries no precision or scale; a float output is a field-only type the engine refuses for measures.
/// </summary>
public sealed class ModelMeasureValidatorTests
{
    /// <summary>The validator.</summary>
    private readonly ModelMeasureValidator _validator = new();

    /// <summary>A double output without a precision or scale passes.</summary>
    [Fact]
    public void DoubleOutput_Passes()
    {
        _validator.TestValidate(Measure(MeasureOutputDataTypes.Double, null, null)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A double output with a precision and scale fails on both.</summary>
    [Fact]
    public void DoubleOutput_ForbidsPrecisionAndScale()
    {
        var result = _validator.TestValidate(Measure(MeasureOutputDataTypes.Double, 20, 4));

        result.ShouldHaveValidationErrorFor(input => input.OutputPrecision);
        result.ShouldHaveValidationErrorFor(input => input.OutputScale);
    }

    /// <summary>A float output fails on its type.</summary>
    [Fact]
    public void FloatOutput_IsRejected()
    {
        _validator.TestValidate(Measure(DataTypes.Float, null, null)).ShouldHaveValidationErrorFor(input => input.OutputDataType);
    }

    /// <summary>A rate calculated measure with the given output type.</summary>
    private static ModelMeasure Measure(string outputDataType, int? precision, int? scale)
    {
        return new ModelMeasure
        {
            Key = "cpr",
            DisplayName = "CPR",
            Formula = "[m.prepaid] / [m.balance]",
            OutputDataType = outputDataType,
            OutputPrecision = precision,
            OutputScale = scale,
            RoundingMode = null,
            UnitKey = null,
            DisplayFormat = "percent"
        };
    }
}

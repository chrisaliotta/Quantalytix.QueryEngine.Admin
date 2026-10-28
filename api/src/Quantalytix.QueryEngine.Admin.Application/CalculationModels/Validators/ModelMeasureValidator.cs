using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a calculated measure's fields: a well-formed key, a formula that parses and reads only <c>[m.KEY]</c> references and
/// parameters, and a known output type with a decimal's precision and scale.
/// </summary>
public sealed class ModelMeasureValidator : AbstractValidator<ModelMeasure>
{
    /// <summary>Declares the rules.</summary>
    public ModelMeasureValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Formula).Cascade(CascadeMode.Stop).NotEmpty().MustFitVarchar(4096).MustBeModelFormula();
        RuleFor(input => input.OutputDataType).MustBeIn(MeasureOutputDataTypes.Definition);
        RuleFor(input => input.RoundingMode).MustBeIn(RoundingModes.Definition);
        RuleFor(input => input.UnitKey).MustFitVarchar(64);
        RuleFor(input => input.DisplayFormat).NotEmpty().MustFitVarchar(32);
        When(input => input.OutputDataType == MeasureOutputDataTypes.Decimal, () =>
        {
            RuleFor(input => input.OutputPrecision).MustBeDecimalPrecision();
            RuleFor(input => input.OutputScale).MustBeDecimalScale(input => input.OutputPrecision, "output precision");
        }).Otherwise(() =>
        {
            RuleFor(input => input.OutputPrecision).Null().WithMessage("Only decimal outputs have a precision, so '{PropertyName}' must be empty.");
            RuleFor(input => input.OutputScale).Null().WithMessage("Only decimal outputs have a scale, so '{PropertyName}' must be empty.");
        });
    }
}

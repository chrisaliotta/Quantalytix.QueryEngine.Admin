using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a calculated measure: a key free among the version's variables and calculated measures, a formula that reads only
/// model keys and the context parameters, and a known output type with a decimal's precision and scale.
/// </summary>
public sealed class CalculatedMeasureInputValidator : AbstractValidator<CalculatedMeasureInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model version.</param>
    public CalculatedMeasureInputValidator(ModelKeys keys)
    {
        RuleFor(input => input.MeasureKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(keys.MetricKeyFreeAsync)
            .WithMessage(input => $"The model version already has a variable or calculated measure with key '{input.MeasureKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.ExpressionText).Cascade(CascadeMode.Stop).NotEmpty().MustFitVarchar(4096).MustBeCalculatedMeasureFormula();
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

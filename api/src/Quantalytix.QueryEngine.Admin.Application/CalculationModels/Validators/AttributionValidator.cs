using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a metric's attribution: an optional expression that parses and reads only model keys and parameters, and drivers with
/// well-formed keys, names and inputs. Whether the metric, the expression's keys, the inputs and the weight variable resolve in the
/// model, and whether the drivers partition the expression's inputs, are model rules, because later edits can change every answer.
/// </summary>
public sealed class AttributionValidator : AbstractValidator<Attribution>
{
    /// <summary>The key the engine names a metric's residual column by, which no driver may take.</summary>
    private const string ResidualKey = "residual";

    /// <summary>Declares the rules.</summary>
    public AttributionValidator()
    {
        RuleFor(input => input.MetricKey).NotEmpty();
        RuleFor(input => input.Expression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(4096)
            .MustBeModelFormula()
            .When(input => input.Expression is not null);
        RuleFor(input => input.WeightVariableKey).NotEmpty().When(input => input.WeightVariableKey is not null);
        RuleForEach(input => input.Drivers).ChildRules(driver =>
        {
            driver.RuleFor(input => input.Key)
                .Cascade(CascadeMode.Stop)
                .MustBeKey(128)
                .NotEqual(ResidualKey)
                .WithMessage($"'{{PropertyName}}' cannot be '{ResidualKey}', which names the part of the change no driver explains.");
            driver.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
            driver.RuleFor(input => input.Inputs)
                .NotEmpty()
                .Must(keys => keys.Distinct(StringComparer.Ordinal).Count() == keys.Count)
                .WithMessage("'{PropertyName}' must list each input once.");
            driver.RuleForEach(input => input.Inputs).NotEmpty();
        });
    }
}

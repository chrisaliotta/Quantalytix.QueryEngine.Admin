using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a metric's attribution: an optional expression that reads only model keys and the context parameters, and drivers with
/// well-formed keys, names and inputs. Whether the metric, the expression's keys, the inputs and the weight variable resolve in the
/// model, and whether the drivers partition the expression's inputs, are model rules, because later edits to the model can change
/// every answer.
/// </summary>
public sealed class MetricAttributionInputValidator : AbstractValidator<MetricAttributionInput>
{
    /// <summary>The key the engine names a metric's residual column by, which no driver may take.</summary>
    private const string ResidualKey = "residual";

    /// <summary>Declares the rules.</summary>
    public MetricAttributionInputValidator()
    {
        RuleFor(input => input.ExpressionText)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(4096)
            .MustBeModelFormula()
            .When(input => input.ExpressionText is not null);
        RuleForEach(input => input.Drivers).ChildRules(driver =>
        {
            driver.RuleFor(input => input.DriverKey)
                .Cascade(CascadeMode.Stop)
                .MustBeKey(128)
                .NotEqual(ResidualKey)
                .WithMessage($"'{{PropertyName}}' cannot be '{ResidualKey}', which names the part of the change no driver explains.");
            driver.RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
            driver.RuleFor(input => input.InputKeys)
                .NotEmpty()
                .Must(keys => keys.Distinct(StringComparer.Ordinal).Count() == keys.Count)
                .WithMessage("'{PropertyName}' must list each input once.");
            driver.RuleForEach(input => input.InputKeys).NotEmpty();
        });
    }
}

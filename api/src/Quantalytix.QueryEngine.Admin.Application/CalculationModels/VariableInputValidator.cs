using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a variable: a key free among the model's variables and calculated measures, a known missing behavior and its display metadata.</summary>
public sealed class VariableInputValidator : AbstractValidator<VariableInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model.</param>
    public VariableInputValidator(ModelKeys keys)
    {
        RuleFor(input => input.VariableKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(keys.MetricKeyFreeAsync)
            .WithMessage(input => $"The model already has a variable or calculated measure with key '{input.VariableKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.VariableRole).NotEmpty().MustFitVarchar(64);
        RuleFor(input => input.MissingBehavior).MustBeIn(VariableMissingBehaviors.Definition);
        RuleFor(input => input.DisplayFormat).NotEmpty().MustFitVarchar(32);
        RuleFor(input => input.UnitKey).MustFitVarchar(64);
    }
}

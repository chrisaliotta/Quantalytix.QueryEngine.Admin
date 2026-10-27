using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a variable's fields: a well-formed key, a known missing behavior and its display metadata.</summary>
public sealed class ModelVariableValidator : AbstractValidator<ModelVariable>
{
    /// <summary>Declares the rules.</summary>
    public ModelVariableValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.MissingBehavior).MustBeIn(VariableMissingBehaviors.Definition);
        RuleFor(input => input.DisplayFormat).NotEmpty().MustFitVarchar(32);
        RuleFor(input => input.UnitKey).MustFitVarchar(64);
    }
}

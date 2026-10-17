using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a variable: a key free among the version's variables and calculated measures, known scope and missing behavior,
/// and a target member exactly when the scope reads one. Whether the target is a member of the report dimension depends on
/// the release, which checks it.
/// </summary>
public sealed class VariableInputValidator : AbstractValidator<VariableInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model version.</param>
    public VariableInputValidator(ModelKeys keys)
    {
        RuleFor(input => input.VariableKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(keys.MetricKeyFreeAsync)
            .WithMessage(input => $"The model version already has a variable or calculated measure with key '{input.VariableKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.VariableRole).NotEmpty().MustFitVarchar(64);
        RuleFor(input => input.SelectionScope).MustBeIn(SelectionScopes.Definition);
        RuleFor(input => input.MissingBehavior).MustBeIn(VariableMissingBehaviors.Definition);
        RuleFor(input => input.TargetMemberKey)
            .Null()
            .WithMessage("An all_mapped variable reads each line's own node, so '{PropertyName}' must be empty.")
            .When(input => input.SelectionScope == SelectionScopes.AllMapped);
        RuleFor(input => input.TargetMemberKey)
            .NotEmpty()
            .WithMessage(input => $"A {input.SelectionScope} variable reads one target member, so '{{PropertyName}}' is required.")
            .MustFitVarchar(128)
            .When(input => input.SelectionScope != SelectionScopes.AllMapped);
    }
}

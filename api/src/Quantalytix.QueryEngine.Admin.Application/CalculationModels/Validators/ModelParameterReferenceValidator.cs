using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks the key a parameter write names: a stable key, and neither <c>periodDays</c> nor <c>yearDays</c>, which every formula reads
/// for the period a report evaluates it for.
/// </summary>
public sealed class ModelParameterReferenceValidator : AbstractValidator<ModelParameterReference>
{
    /// <summary>Declares the rules.</summary>
    public ModelParameterReferenceValidator()
    {
        RuleFor(reference => reference.ParameterKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .Must(key => !FormulaContext.ParameterNames.Contains(key))
            .WithMessage(reference => $"Every formula reads @{reference.ParameterKey} for the period a report evaluates it for, so no model declares it.");
    }
}

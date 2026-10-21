using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks the editable attributes of a calculation model; <see cref="NewCalculationModelInputValidator"/> includes these rules.
/// Whether the model's formulas call only what its formula language has is the <c>formulas_fit_language</c> model rule's concern.
/// </summary>
public sealed class CalculationModelInputValidator : AbstractValidator<CalculationModelInput>
{
    /// <summary>Declares the rules.</summary>
    public CalculationModelInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.CurrencyCode).Matches("^[A-Z]{3}$").WithMessage("'{PropertyName}' must be an ISO 4217 code of three capital letters, such as 'USD'.");
        RuleFor(input => input.FormulaLanguageVersion).MustBeIn(FormulaLanguageVersions.Definition);
        RuleFor(input => input.AttributionMethod).MustBeIn(AttributionMethods.Definition);
    }
}

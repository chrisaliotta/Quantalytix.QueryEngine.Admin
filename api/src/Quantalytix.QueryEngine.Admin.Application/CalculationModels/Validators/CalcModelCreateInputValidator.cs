using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a new model's fields; whether its key is taken is the create command's conflict.</summary>
public sealed class CalcModelCreateInputValidator : AbstractValidator<CalcModelCreateInput>
{
    /// <summary>The length of <c>qe_calculation_model.model_key</c>.</summary>
    private const int KeyLength = 128;

    /// <summary>Declares the rules.</summary>
    public CalcModelCreateInputValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeDottedKey(KeyLength);
        RuleFor(input => input.DisplayName).MustBeModelName();
        RuleFor(input => input.CurrencyCode).MustBeCurrencyCode();
        RuleFor(input => input.FormulaLanguage).MustBeIn(FormulaLanguageVersions.Definition);
        RuleFor(input => input.AttributionMethod).MustBeIn(AttributionMethods.Definition);
    }
}

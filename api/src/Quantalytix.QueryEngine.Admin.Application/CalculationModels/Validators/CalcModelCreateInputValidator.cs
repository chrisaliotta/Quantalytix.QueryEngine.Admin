using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a new model's fields; whether its key is taken is the create command's conflict.</summary>
public sealed class CalcModelCreateInputValidator : AbstractValidator<CalcModelCreateInput>
{
    /// <summary>A dotted key: a letter, then letters, digits, underscores or dots (OI-29), matching the seed's <c>bank.margin_model</c>.</summary>
    private const string DottedKeyPattern = @"^[A-Za-z][A-Za-z0-9_.]*\z";

    /// <summary>Declares the rules.</summary>
    public CalcModelCreateInputValidator()
    {
        RuleFor(input => input.Key)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(DottedKeyPattern)
            .WithMessage("'{PropertyName}' must start with a letter and contain only letters, digits, '_' and '.'.");
        RuleFor(input => input.DisplayName).MustBeModelName();
        RuleFor(input => input.CurrencyCode).MustBeCurrencyCode();
        RuleFor(input => input.FormulaLanguage).MustBeIn(FormulaLanguageVersions.Definition);
        RuleFor(input => input.AttributionMethod).MustBeIn(AttributionMethods.Definition);
    }
}

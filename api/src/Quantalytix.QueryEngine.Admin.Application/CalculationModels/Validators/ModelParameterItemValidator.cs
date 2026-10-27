using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a parameter's fields (C5): a stable key that is neither <c>periodDays</c> nor <c>yearDays</c>, which every formula reads for
/// the period a report evaluates it for; a number parameter has a number default and nothing else; a text parameter has a text
/// default and optionally the texts it may take, each once, the default among them. Whether the model's formulas and its reports'
/// rows still read and set the parameter as its type says is the model gate's concern.
/// </summary>
public sealed class ModelParameterItemValidator : AbstractValidator<ModelParameterItem>
{
    /// <summary>Declares the rules.</summary>
    public ModelParameterItemValidator()
    {
        RuleFor(input => input.Key)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .Must(key => !FormulaContext.ParameterNames.Contains(key))
            .WithMessage(input => $"Every formula reads @{input.Key} for the period a report evaluates it for, so no model declares it.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.ValueType).MustBeIn(ParameterValueTypes.Definition);
        RuleFor(input => input.Description).MustFitVarchar(1024);
        When(input => input.ValueType == ParameterValueTypes.Number, () =>
        {
            RuleFor(input => input.DefaultNumber).NotNull().PrecisionScale(38, 12, true);
            RuleFor(input => input.AllowedValues).Null().WithMessage("A number parameter takes any number, so '{PropertyName}' must be empty.");
            RuleFor(input => input.DefaultText).Null().WithMessage("A number parameter's default is a number, so '{PropertyName}' must be empty.");
        });
        When(input => input.ValueType == ParameterValueTypes.Text, () =>
        {
            RuleFor(input => input.DefaultText)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .MustFitVarchar(256)
                .Must((input, text) => input.AllowedValues is null || input.AllowedValues.Contains(text!, StringComparer.Ordinal))
                .WithMessage("'{PropertyName}' must be one of the allowed values.");
            RuleFor(input => input.DefaultNumber).Null().WithMessage("A text parameter's default is text, so '{PropertyName}' must be empty.");
            RuleFor(input => input.AllowedValues)
                .NotEmpty()
                .Must(values => values!.Distinct(StringComparer.Ordinal).Count() == values!.Count)
                .WithMessage("'{PropertyName}' must list each value once.")
                .When(input => input.AllowedValues is not null);
            RuleForEach(input => input.AllowedValues).NotEmpty().MustFitVarchar(256);
        });
    }
}

using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks a model version's attributes; <see cref="NewModelVersionInputValidator"/> includes these rules.</summary>
public sealed class ModelVersionInputValidator : AbstractValidator<ModelVersionInput>
{
    /// <summary>Declares the rules.</summary>
    public ModelVersionInputValidator()
    {
        RuleFor(input => input.CurrencyCode).Matches("^[A-Z]{3}$").WithMessage("'{PropertyName}' must be an ISO 4217 code of three capital letters, such as 'USD'.");
    }
}

using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Checks the identity attributes of a calculation model; <see cref="NewCalculationModelInputValidator"/> includes these rules.</summary>
public sealed class CalculationModelInputValidator : AbstractValidator<CalculationModelInput>
{
    /// <summary>Declares the rules.</summary>
    public CalculationModelInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
    }
}

using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a dimension version's attributes: a known range data type and a half-open validity window whose end is later
/// than its start. <see cref="NewDimensionVersionInputValidator"/> includes these rules.
/// </summary>
public sealed class DimensionVersionInputValidator : AbstractValidator<DimensionVersionInput>
{
    /// <summary>Declares the rules.</summary>
    public DimensionVersionInputValidator()
    {
        RuleFor(input => input.RangeDataType).MustBeIn(RangeDataTypes.Definition);
        RuleFor(input => input.ValidFrom).MustBeUtc();
        RuleFor(input => input.ValidTo).MustBeUtc();
        RuleFor(input => input.ValidTo)
            .Must((input, validTo) => validTo > input.ValidFrom)
            .WithMessage("'{PropertyName}' must be later than 'validFrom'.")
            .When(input => input.ValidFrom is not null && input.ValidTo is not null);
    }
}

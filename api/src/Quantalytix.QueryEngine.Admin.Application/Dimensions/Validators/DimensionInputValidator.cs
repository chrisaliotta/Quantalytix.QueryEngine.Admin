using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks the identity attributes of a dimension update; <see cref="NewDimensionInputValidator"/> includes these rules.</summary>
public sealed class DimensionInputValidator : AbstractValidator<DimensionInput>
{
    /// <summary>Declares the rules.</summary>
    public DimensionInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
    }
}

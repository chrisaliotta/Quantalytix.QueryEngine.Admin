using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks the editable attributes of a dimension role; <see cref="NewDimensionRoleInputValidator"/> includes these rules.</summary>
public sealed class DimensionRoleInputValidator : AbstractValidator<DimensionRoleInput>
{
    /// <summary>Declares the rules.</summary>
    public DimensionRoleInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}

using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new dimension's shape: a well-formed key, a known kind, a range data type exactly when the kind is <c>range</c>, and a
/// first role exactly when the kind is not <c>time</c> (M6). A taken key is a conflict, which the command answers.
/// </summary>
public sealed class CreateDimensionInputValidator : AbstractValidator<CreateDimensionInput>
{
    /// <summary>Declares the rules.</summary>
    public CreateDimensionInputValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeKey(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Kind).MustBeIn(DimensionKinds.Definition);
        RuleFor(input => input.RangeDataType).MustBeIn(RangeDataTypes.Definition);
        RuleFor(input => input.RangeDataType)
            .NotNull()
            .WithMessage("'{PropertyName}' is required for a range dimension.")
            .When(input => input.Kind == DimensionKinds.Range);
        RuleFor(input => input.RangeDataType)
            .Null()
            .WithMessage("'{PropertyName}' must be null unless the dimension kind is range.")
            .When(input => input.Kind != DimensionKinds.Range);
        When(input => input.Kind == DimensionKinds.Time, () =>
        {
            RuleFor(input => input.FirstRole).Null().WithMessage("A time dimension has no roles, so '{PropertyName}' must be null.");
        }).Otherwise(() =>
        {
            RuleFor(input => input.FirstRole).NotNull().WithMessage("'{PropertyName}' is required, because bindings bind a role of the dimension.");
            RuleFor(input => input.FirstRole!.Key).Cascade(CascadeMode.Stop).MustBeKey(128).When(input => input.FirstRole is not null);
            RuleFor(input => input.FirstRole!.DisplayName).NotEmpty().MustFitVarchar(256).When(input => input.FirstRole is not null);
        });
    }
}

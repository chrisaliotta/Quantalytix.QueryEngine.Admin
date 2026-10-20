using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new dimension: a unique, well-formed key, a known kind, a range data type exactly when the kind is <c>range</c>,
/// because only range dimensions assign members from typed bands, and a first role exactly when the kind is not <c>time</c>,
/// because time bindings name their dimension and every other binding binds a role.
/// </summary>
public sealed class NewDimensionInputValidator : AbstractValidator<NewDimensionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensions">Reads existing dimension keys.</param>
    /// <param name="roles">Reads existing role keys.</param>
    public NewDimensionInputValidator(ITableGateway<DimensionRecord> dimensions, ITableGateway<DimensionRoleRecord> roles)
    {
        Include(new DimensionInputValidator());
        RuleFor(input => input.DimensionKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await dimensions.ListAsync(new { DimensionKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A dimension with key '{input.DimensionKey}' already exists.");
        RuleFor(input => input.DimensionKind).MustBeIn(DimensionKinds.Definition);
        RuleFor(input => input.RangeDataType)
            .NotNull()
            .WithMessage("'{PropertyName}' is required for a range dimension.")
            .When(input => input.DimensionKind == DimensionKinds.Range);
        RuleFor(input => input.RangeDataType)
            .Null()
            .WithMessage("'{PropertyName}' must be null unless the dimension kind is range.")
            .When(input => input.DimensionKind != DimensionKinds.Range);
        RuleFor(input => input.RangeDataType).MustBeIn(RangeDataTypes.Definition);
        When(input => input.DimensionKind == DimensionKinds.Time, () =>
        {
            RuleFor(input => input.DefaultRole).Null().WithMessage("A time dimension has no roles, so '{PropertyName}' must be absent.");
        }).Otherwise(() =>
        {
            RuleFor(input => input.DefaultRole).NotNull().WithMessage("'{PropertyName}' is required, because bindings bind a role of the dimension.");
            RuleFor(input => input.DefaultRole!).SetValidator(new NewDimensionRoleInputValidator(roles));
        });
    }
}

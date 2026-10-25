using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a tree reorder: each member is placed once and never under itself. Longer parent cycles are the
/// <c>hierarchy_acyclic</c> publication rule's concern.
/// </summary>
public sealed class MemberOrderInputValidator : AbstractValidator<MemberOrderInput>
{
    /// <summary>Declares the rules.</summary>
    public MemberOrderInputValidator()
    {
        RuleFor(input => input.Items)
            .Must(items => items.DistinctBy(item => item.DimensionMemberId).Count() == items.Count)
            .WithMessage("'{PropertyName}' must place each member once.");
        RuleForEach(input => input.Items)
            .Must(item => item.ParentDimensionMemberId != item.DimensionMemberId)
            .WithMessage((_, item) => $"Member {item.DimensionMemberId} cannot be its own parent.");
    }
}

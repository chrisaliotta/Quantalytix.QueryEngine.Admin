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
            .Must(items => items.DistinctBy(item => item.MemberId).Count() == items.Count)
            .WithMessage("'{PropertyName}' must place each member once.");
        RuleForEach(input => input.Items)
            .Must(item => item.ParentMemberId != item.MemberId)
            .WithMessage((_, item) => $"Member {item.MemberId} cannot be its own parent.");
    }
}

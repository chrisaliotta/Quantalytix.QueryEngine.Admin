using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a member's editable attributes; <see cref="NewMemberInputValidator"/> includes these rules.</summary>
public sealed class MemberInputValidator : AbstractValidator<MemberInput>
{
    /// <summary>Declares the rules.</summary>
    public MemberInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(512);
        RuleFor(input => input.Description).MaximumLength(1024);
        RuleFor(input => input.Properties).MustBeJson();
    }
}

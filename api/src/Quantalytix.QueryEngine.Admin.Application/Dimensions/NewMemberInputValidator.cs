using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Checks a new member: its attributes and its key. Member keys are business codes, so any characters are allowed except
/// whitespace at either end, which would never match a trimmed source value.
/// </summary>
public sealed class NewMemberInputValidator : AbstractValidator<NewMemberInput>
{
    /// <summary>Declares the rules.</summary>
    public NewMemberInputValidator()
    {
        Include(new MemberInputValidator());
        RuleFor(input => input.MemberKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .Must(key => key.Trim() == key)
            .WithMessage("'{PropertyName}' must not start or end with whitespace.");
    }
}

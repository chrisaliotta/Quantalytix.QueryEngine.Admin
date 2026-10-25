using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Checks a member's editable attributes; <see cref="NewMemberInputValidator"/> includes these rules.</summary>
public sealed class MemberInputValidator : AbstractValidator<MemberInput>
{
    /// <summary>Declares the rules.</summary>
    public MemberInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(512);
        RuleFor(input => input.Description).MustFitVarchar(1024);
        RuleFor(input => input.Properties).MustBeJson();
    }
}

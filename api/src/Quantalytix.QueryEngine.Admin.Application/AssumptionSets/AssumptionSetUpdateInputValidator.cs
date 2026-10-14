using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks the settings of a draft assumption set: a display name and a UTC effective time.</summary>
public sealed class AssumptionSetUpdateInputValidator : AbstractValidator<AssumptionSetUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    public AssumptionSetUpdateInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(input => input.Description).MaximumLength(1024);
        RuleFor(input => input.EffectiveAt).MustBeUtc();
    }
}

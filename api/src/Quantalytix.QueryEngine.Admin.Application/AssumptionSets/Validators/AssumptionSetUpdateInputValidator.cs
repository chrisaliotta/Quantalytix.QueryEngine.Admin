using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks an assumption set's labels: a display name and a description that fit their columns.</summary>
public sealed class AssumptionSetUpdateInputValidator : AbstractValidator<AssumptionSetUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    public AssumptionSetUpdateInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}

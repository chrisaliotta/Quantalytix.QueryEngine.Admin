using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks a capture: a well-formed, unused key and a display name.</summary>
public sealed class AssumptionCaptureInputValidator : AbstractValidator<AssumptionCaptureInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sets">The assumption set table, read for the uniqueness check.</param>
    public AssumptionCaptureInputValidator(ITableGateway<AssumptionSetRecord> sets)
    {
        RuleFor(input => input.AssumptionSetKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync(async (key, cancellationToken) => (await sets.ListAsync(new { AssumptionSetKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"An assumption set with key '{input.AssumptionSetKey}' already exists.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}

using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks a capture request: it names at least one dimension, and every dimension exists.</summary>
public sealed class AssumptionCaptureInputValidator : AbstractValidator<AssumptionCaptureInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensions">The dimension table, read to check that every dimension exists.</param>
    public AssumptionCaptureInputValidator(ITableGateway<DimensionRecord> dimensions)
    {
        RuleFor(input => input.DimensionIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .CustomAsync(async (dimensionIds, context, cancellationToken) =>
            {
                var existing = await dimensions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken);
                foreach (var missing in dimensionIds.Except(existing.Select(dimension => dimension.DimensionId)))
                    context.AddFailure($"Dimension {missing} does not exist.");
            });
    }
}

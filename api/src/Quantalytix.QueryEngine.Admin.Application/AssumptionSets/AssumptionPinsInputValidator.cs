using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Checks the shape of an assumption set's pins: every dimension exists and appears at most once. Whether a pinned version
/// belongs to its dimension and is published is for the assumption set's publication rules to decide.
/// </summary>
public sealed class AssumptionPinsInputValidator : AbstractValidator<AssumptionPinsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensions">The dimension table, read to check that every dimension exists.</param>
    public AssumptionPinsInputValidator(ITableGateway<DimensionRecord> dimensions)
    {
        RuleForEach(input => input.Dimensions)
            .Must((input, pin) => input.Dimensions.Count(other => other.DimensionId == pin.DimensionId) == 1)
            .WithMessage((_, pin) => $"Dimension {pin.DimensionId} appears more than once.");
        RuleFor(input => input.Dimensions).CustomAsync(async (pins, context, cancellationToken) =>
        {
            long[] dimensionIds = [.. pins.Select(pin => pin.DimensionId)];
            var existing = await dimensions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken);
            foreach (var missing in dimensionIds.Except(existing.Select(dimension => dimension.DimensionId)))
                context.AddFailure($"Dimension {missing} does not exist.");
        });
    }
}

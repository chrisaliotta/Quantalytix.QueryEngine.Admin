using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks a calculation dimension: a key free among the version's calculation dimensions, and a canonical dimension that
/// exists for a <c>canonical</c> one or a raw value type for a <c>scalar</c> one, never both.
/// </summary>
public sealed class CalculationDimensionInputValidator : AbstractValidator<CalculationDimensionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Checks the key against the route's model version.</param>
    /// <param name="calculationDimensions">Reads the version's calculation dimension keys.</param>
    /// <param name="dimensions">Reads the canonical dimension.</param>
    public CalculationDimensionInputValidator(
        ModelKeys keys,
        ITableGateway<CalculationDimensionRecord> calculationDimensions,
        ITableGateway<DimensionRecord> dimensions)
    {
        RuleFor(input => input.DimensionKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(calculationDimensions, new { ModelVersionId = keys.VersionId, DimensionKey = key },
                dimension => dimension.CalculationDimensionId, cancellationToken))
            .WithMessage(input => $"The model version already has a calculation dimension with key '{input.DimensionKey}'.");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.ValueKind).MustBeIn(CalculationValueKinds.Definition);
        When(input => input.ValueKind == CalculationValueKinds.Canonical, () =>
        {
            RuleFor(input => input.DimensionId)
                .NotNull()
                .WithMessage("A canonical calculation dimension names its canonical dimension, so '{PropertyName}' is required.")
                .MustAsync(async (dimensionId, cancellationToken) => (await dimensions.ListAsync(new { DimensionId = dimensionId }, cancellationToken)).Count == 1)
                .WithMessage(input => $"Dimension {input.DimensionId} does not exist.");
            RuleFor(input => input.ValueDataType).Null().WithMessage("A canonical calculation dimension resolves members, so '{PropertyName}' must be empty.");
        });
        When(input => input.ValueKind == CalculationValueKinds.Scalar, () =>
        {
            RuleFor(input => input.ValueDataType)
                .NotNull()
                .WithMessage("A scalar calculation dimension shares one raw value type, so '{PropertyName}' is required.")
                .MustBeIn(DataTypes.Definition);
            RuleFor(input => input.DimensionId).Null().WithMessage("A scalar calculation dimension reads raw values, so '{PropertyName}' must be empty.");
        });
    }
}

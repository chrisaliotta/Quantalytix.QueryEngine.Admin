using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a calculation dimension: a key free among the version's calculation dimensions, and either a canonical dimension that
/// exists with a grouping of its members, or a raw value type for a <c>scalar</c> one, never both. Whether the grouping keys name
/// a level or member of the dimension's published version is the <c>calculation_grouping_resolve</c> rule's concern.
/// </summary>
public sealed class CalculationDimensionInputValidator : AbstractValidator<CalculationDimensionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="keys">Names the route's report version and edited dimension.</param>
    /// <param name="calculationDimensions">Reads the version's calculation dimension keys.</param>
    /// <param name="dimensions">Reads the canonical dimension.</param>
    public CalculationDimensionInputValidator(
        VersionKeys keys,
        ITableGateway<CalculationDimensionRecord> calculationDimensions,
        ITableGateway<DimensionRecord> dimensions)
    {
        RuleFor(input => input.DimensionKey)
            .Cascade(CascadeMode.Stop)
            .MustBeKey(128)
            .MustAsync((key, cancellationToken) => keys.KeyFreeAsync(calculationDimensions, new { ReportVersionId = keys.VersionId, DimensionKey = key },
                dimension => dimension.CalculationDimensionId, cancellationToken))
            .WithMessage(input => $"The report version already has a calculation dimension with key '{input.DimensionKey}'.");
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
            Include(new GroupingInputValidator<CalculationDimensionInput>(128));
        });
        When(input => input.ValueKind == CalculationValueKinds.Scalar, () =>
        {
            RuleFor(input => input.ValueDataType)
                .NotNull()
                .WithMessage("A scalar calculation dimension shares one raw value type, so '{PropertyName}' is required.")
                .MustBeIn(DataTypes.Definition);
            RuleFor(input => input.DimensionId).Null().WithMessage("A scalar calculation dimension reads raw values, so '{PropertyName}' must be empty.");
            RuleFor(input => input.GroupingMode).Null().WithMessage(NoMembers);
            RuleFor(input => input.GroupingLevelKey).Null().WithMessage(NoMembers);
            RuleFor(input => input.GroupingAncestorMemberKey).Null().WithMessage(NoMembers);
            RuleFor(input => input.GroupingDepth).Null().WithMessage(NoMembers);
        });
    }

    /// <summary>The message of a grouping property a scalar calculation dimension sets.</summary>
    private const string NoMembers = "A scalar calculation dimension has no members to group, so '{PropertyName}' must be empty.";
}

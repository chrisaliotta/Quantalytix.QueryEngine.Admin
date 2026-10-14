using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Accepts a range band route only for a version of a <c>range</c> dimension, the only kind whose members come from bands.</summary>
public sealed class RangeVersionReferenceValidator : AbstractValidator<RangeVersionReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the version.</param>
    /// <param name="dimensions">Reads the version's dimension.</param>
    public RangeVersionReferenceValidator(ITableGateway<DimensionVersionRecord> versions, ITableGateway<DimensionRecord> dimensions)
    {
        RuleFor(reference => reference.DimensionVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
            {
                var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
                var dimension = await dimensions.GetAsync(new { version.DimensionId }, cancellationToken);
                return dimension.DimensionKind == DimensionKinds.Range;
            })
            .WithMessage(reference => $"Dimension version {reference.DimensionVersionId} belongs to a dimension whose kind is not range, so it has no range bands.");
    }
}

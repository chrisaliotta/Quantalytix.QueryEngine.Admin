using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>Accepts a measure route only when the measure belongs to the dataset version named in the same route.</summary>
public sealed class MeasureReferenceValidator : AbstractValidator<MeasureReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="measures">The tenant's measures.</param>
    public MeasureReferenceValidator(ITableGateway<MeasureRecord> measures)
    {
        RuleFor(reference => reference.MeasureId)
            .MustAsync(async (reference, measureId, cancellationToken) =>
                (await measures.ListAsync(new { MeasureId = measureId, DatasetVersionId = reference.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Measure {reference.MeasureId} is not a measure of dataset version {reference.VersionId}.");
    }
}

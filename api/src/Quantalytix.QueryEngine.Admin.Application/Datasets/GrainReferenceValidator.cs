using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Accepts a grain route only when the grain belongs to the dataset version named in the same route.</summary>
public sealed class GrainReferenceValidator : AbstractValidator<GrainReference>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="grains">The tenant's dataset grains.</param>
    public GrainReferenceValidator(ITableGateway<DatasetGrainRecord> grains)
    {
        RuleFor(reference => reference.DatasetGrainId)
            .MustAsync(async (reference, datasetGrainId, cancellationToken) =>
                (await grains.ListAsync(new { DatasetGrainId = datasetGrainId, DatasetVersionId = reference.VersionId }, cancellationToken)).Count == 1)
            .WithMessage(reference => $"Grain {reference.DatasetGrainId} is not a grain of dataset version {reference.VersionId}.");
    }
}

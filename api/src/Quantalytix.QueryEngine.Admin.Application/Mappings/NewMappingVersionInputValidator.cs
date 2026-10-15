using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a new mapping version: the dimension version it pins exists, and the version it clones is a version of the
/// route's mapping set. That the dimension version belongs to the set's dimension is the
/// <c>dimension_version_belongs_to_set_dimension</c> rule's to check.
/// </summary>
public sealed class NewMappingVersionInputValidator : AbstractValidator<NewMappingVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensionVersions">Reads the pinned dimension version.</param>
    /// <param name="mappingVersions">Reads the clone source.</param>
    /// <param name="route">Names the mapping set the new version belongs to.</param>
    public NewMappingVersionInputValidator(
        ITableGateway<DimensionVersionRecord> dimensionVersions,
        ITableGateway<MappingVersionRecord> mappingVersions,
        IRequestRoute route)
    {
        RuleFor(input => input.DimensionVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await dimensionVersions.ListAsync(new { DimensionVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension version {input.DimensionVersionId} does not exist.");
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await mappingVersions.ListAsync(new { MappingVersionId = versionId, MappingSetId = route.Id("mappingSetId") }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Mapping version {input.CloneFromVersionId} is not a version of this mapping set.")
            .When(input => input.CloneFromVersionId is not null);
    }
}

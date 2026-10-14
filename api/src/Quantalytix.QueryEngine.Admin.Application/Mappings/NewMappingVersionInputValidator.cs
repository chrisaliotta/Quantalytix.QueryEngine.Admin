using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a new mapping version: the dimension version it pins and the version it clones exist. That the dimension version
/// belongs to the set's dimension needs the route too, so the <c>dimension_version_belongs_to_set_dimension</c> rule checks it.
/// </summary>
public sealed class NewMappingVersionInputValidator : AbstractValidator<NewMappingVersionInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="dimensionVersions">Reads the pinned dimension version.</param>
    /// <param name="mappingVersions">Reads the clone source.</param>
    public NewMappingVersionInputValidator(
        ITableGateway<DimensionVersionRecord> dimensionVersions,
        ITableGateway<MappingVersionRecord> mappingVersions)
    {
        RuleFor(input => input.DimensionVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await dimensionVersions.ListAsync(new { DimensionVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Dimension version {input.DimensionVersionId} does not exist.");
        RuleFor(input => input.CloneFromVersionId)
            .MustAsync(async (versionId, cancellationToken) =>
                (await mappingVersions.ListAsync(new { MappingVersionId = versionId }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Mapping version {input.CloneFromVersionId} does not exist.")
            .When(input => input.CloneFromVersionId is not null);
    }
}

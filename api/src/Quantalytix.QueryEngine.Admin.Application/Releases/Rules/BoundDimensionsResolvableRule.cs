using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bound_dimensions_resolvable</c>: the dimension of every binding of every pinned dataset version has a published version,
/// because the active assumption set pins it and the engine resolves the binding's members in it. Time bindings are exempt:
/// the engine derives their calendar periods from the bound field.
/// </summary>
public sealed class BoundDimensionsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every pinned binding whose dimension has no published version.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolvable binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var bound = (await reader.ListBindingsAsync(versionId, cancellationToken))
            .Where(pinned => pinned.Binding.BindingKind != BindingKinds.Time)
            .ToList();
        long[] dimensionIds = [.. bound.Select(pinned => pinned.Binding.DimensionId).Distinct()];
        var published = (await dimensionVersions.ListAsync(new { DimensionId = dimensionIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.DimensionId)
            .ToHashSet();
        var dimensionKeys = (await dimensions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => dimension.DimensionKey);
        return
        [
            .. bound
                .Where(pinned => !published.Contains(pinned.Binding.DimensionId))
                .Select(pinned => new RuleViolation
                {
                    Code = "bound_dimensions_resolvable",
                    Message = $"Binding '{pinned.Name}' of dataset '{pinned.DatasetKey}' binds dimension '{dimensionKeys[pinned.Binding.DimensionId]}', which has no published version."
                })
        ];
    }
}

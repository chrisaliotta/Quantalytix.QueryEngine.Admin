using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bound_dimensions_resolvable</c>: every binding of every pinned dataset version resolves to a published dimension
/// version, because the engine reads published dimension versions only. A binding that pins a version needs that version
/// published; an unpinned binding needs its dimension to have a published version. Unpinned time bindings are exempt: the
/// engine derives their calendar periods from the bound field and selects no version when the dimension has none.
/// </summary>
public sealed class BoundDimensionsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code.</summary>
    private const string Code = "bound_dimensions_resolvable";

    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every pinned binding that cannot resolve to a published dimension version.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolvable binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var bindings = await reader.ListBindingsAsync(versionId, cancellationToken);
        long[] dimensionIds = [.. bindings.Select(pinned => pinned.Binding.DimensionId).Distinct()];
        var published = (await dimensionVersions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken))
            .Where(version => version.Status == PublicationStatuses.Published)
            .ToList();
        var dimensionKeys = (await dimensions.ListAsync(new { DimensionId = dimensionIds }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => dimension.DimensionKey);

        var violations = new List<RuleViolation>();
        foreach (var pinned in bindings)
        {
            var binding = pinned.Binding;
            var subject = $"Binding '{binding.DisplayName}' of dataset '{pinned.DatasetKey}'";
            if (binding.PinnedDimensionVersionId is { } pinnedVersionId)
            {
                if (!published.Any(version => version.DimensionId == binding.DimensionId && version.DimensionVersionId == pinnedVersionId))
                {
                    var message = $"{subject} pins dimension version {pinnedVersionId}, which is not a published version of dimension '{dimensionKeys[binding.DimensionId]}'.";
                    violations.Add(new RuleViolation { Rule = Code, Message = message });
                }
            }
            else if (binding.BindingKind != BindingKinds.Time && !published.Any(version => version.DimensionId == binding.DimensionId))
            {
                var message = $"{subject} binds dimension '{dimensionKeys[binding.DimensionId]}', which has no published version.";
                violations.Add(new RuleViolation { Rule = Code, Message = message });
            }
        }

        return violations;
    }
}

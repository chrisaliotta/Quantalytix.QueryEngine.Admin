using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>mapped_bindings_resolvable</c>: the mapping set of every mapped binding of a pinned dataset version has a published
/// mapping version. The engine maps source keys through the published mapping version of the binding's mapping set and
/// resolved dimension version, so a binding that pins a dimension version needs a published mapping version for that version.
/// </summary>
public sealed class MappedBindingsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<MappingSetRecord> mappingSets)
    : IPublicationRule
{
    /// <summary>The rule code.</summary>
    private const string Code = "mapped_bindings_resolvable";

    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every mapped binding that no published mapping version can serve.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolvable mapped binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var mapped = (await reader.ListBindingsAsync(versionId, cancellationToken))
            .Where(pinned => pinned.Binding.BindingKind == BindingKinds.Mapped)
            .ToList();
        long[] mappingSetIds = [.. mapped.Select(pinned => pinned.Binding.MappingSetId!.Value).Distinct()];
        var published = (await mappingVersions.ListAsync(new { MappingSetId = mappingSetIds }, cancellationToken))
            .Where(version => version.Status == PublicationStatuses.Published)
            .ToList();
        var mappingSetKeys = (await mappingSets.ListAsync(new { MappingSetId = mappingSetIds }, cancellationToken))
            .ToDictionary(mappingSet => mappingSet.MappingSetId, mappingSet => mappingSet.MappingSetKey);

        var violations = new List<RuleViolation>();
        foreach (var pinned in mapped)
        {
            var binding = pinned.Binding;
            var mappingSetId = binding.MappingSetId!.Value;
            var subject = $"Binding '{binding.DisplayName}' of dataset '{pinned.DatasetKey}' maps through mapping set '{mappingSetKeys[mappingSetId]}'";
            if (binding.PinnedDimensionVersionId is { } pinnedVersionId)
            {
                if (!published.Any(version => version.MappingSetId == mappingSetId && version.DimensionVersionId == pinnedVersionId))
                {
                    var message = $"{subject}, which has no published mapping version for its pinned dimension version {pinnedVersionId}.";
                    violations.Add(new RuleViolation { Rule = Code, Message = message });
                }
            }
            else if (!published.Any(version => version.MappingSetId == mappingSetId))
            {
                violations.Add(new RuleViolation { Rule = Code, Message = $"{subject}, which has no published mapping version." });
            }
        }

        return violations;
    }
}

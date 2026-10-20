using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>mapped_bindings_resolvable</c>: the mapping set of every mapped binding of a pinned dataset version has a published
/// version, because the active assumption set pins it and the engine maps source keys through it.
/// </summary>
public sealed class MappedBindingsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<MappingSetRecord> mappingSets)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every mapped binding whose mapping set has no published version.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolvable mapped binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var mapped = (await reader.ListBindingsAsync(versionId, cancellationToken))
            .Where(pinned => pinned.Binding.BindingKind == BindingKinds.Mapped)
            .ToList();
        long[] mappingSetIds = [.. mapped.Select(pinned => pinned.Binding.MappingSetId!.Value).Distinct()];
        var published = (await mappingVersions.ListAsync(new { MappingSetId = mappingSetIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.MappingSetId)
            .ToHashSet();
        var mappingSetKeys = (await mappingSets.ListAsync(new { MappingSetId = mappingSetIds }, cancellationToken))
            .ToDictionary(mappingSet => mappingSet.MappingSetId, mappingSet => mappingSet.MappingSetKey);
        return
        [
            .. mapped
                .Where(pinned => !published.Contains(pinned.Binding.MappingSetId!.Value))
                .Select(pinned => new RuleViolation
                {
                    Rule = "mapped_bindings_resolvable",
                    Message = $"Binding '{pinned.Binding.DisplayName}' of dataset '{pinned.DatasetKey}' maps through mapping set '{mappingSetKeys[pinned.Binding.MappingSetId!.Value]}', which has no published version."
                })
        ];
    }
}

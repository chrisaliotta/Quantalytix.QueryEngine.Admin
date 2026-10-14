using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bridge_bindings_resolvable</c>: every bridge binding of a pinned dataset version uses a published bridge version,
/// because the engine loads published bridge versions only.
/// </summary>
public sealed class BridgeBindingsResolvableRule(ReleaseManifestReader reader, ITableGateway<BridgeVersionRecord> bridgeVersions)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every bridge binding whose bridge version is not published.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per bridge binding on an unpublished bridge version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var bridged = (await reader.ListBindingsAsync(versionId, cancellationToken))
            .Where(pinned => pinned.Binding.BindingKind == BindingKinds.Bridge)
            .ToList();
        var versions = await bridgeVersions.ListAsync(
            new { BridgeVersionId = bridged.Select(pinned => pinned.Binding.BridgeVersionId!.Value).Distinct().ToArray() },
            cancellationToken);
        var published = versions
            .Where(version => version.Status == PublicationStatuses.Published)
            .Select(version => version.BridgeVersionId)
            .ToHashSet();
        return
        [
            .. bridged
                .Where(pinned => !published.Contains(pinned.Binding.BridgeVersionId!.Value))
                .Select(pinned => new RuleViolation
                {
                    Rule = "bridge_bindings_resolvable",
                    Message = $"Binding '{pinned.Binding.DisplayName}' of dataset '{pinned.DatasetKey}' uses bridge version {pinned.Binding.BridgeVersionId}, which is not published."
                })
        ];
    }
}

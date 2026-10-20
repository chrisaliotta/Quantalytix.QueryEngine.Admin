using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bridge_bindings_resolvable</c>: the bridge of every bridge binding of a pinned dataset version has a published version,
/// because the active assumption set pins it and the engine maps rows through it.
/// </summary>
public sealed class BridgeBindingsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeRecord> bridges)
    : IPublicationRule
{
    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every bridge binding whose bridge has no published version.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per bridge binding on a bridge with no published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var bridged = (await reader.ListBindingsAsync(versionId, cancellationToken))
            .Where(pinned => pinned.Binding.BindingKind == BindingKinds.Bridge)
            .ToList();
        long[] bridgeIds = [.. bridged.Select(pinned => pinned.Binding.BridgeId!.Value).Distinct()];
        var published = (await bridgeVersions.ListAsync(new { BridgeId = bridgeIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.BridgeId)
            .ToHashSet();
        var bridgeKeys = (await bridges.ListAsync(new { BridgeId = bridgeIds }, cancellationToken))
            .ToDictionary(bridge => bridge.BridgeId, bridge => bridge.BridgeKey);
        return
        [
            .. bridged
                .Where(pinned => !published.Contains(pinned.Binding.BridgeId!.Value))
                .Select(pinned => new RuleViolation
                {
                    Rule = "bridge_bindings_resolvable",
                    Message = $"Binding '{pinned.Name}' of dataset '{pinned.DatasetKey}' maps through bridge '{bridgeKeys[pinned.Binding.BridgeId!.Value]}', which has no published version."
                })
        ];
    }
}

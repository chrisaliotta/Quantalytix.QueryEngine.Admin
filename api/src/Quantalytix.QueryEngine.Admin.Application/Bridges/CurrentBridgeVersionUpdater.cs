using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Points <c>qe_bridge.current_bridge_version_id</c> at a bridge version when it is published. The pointer is
/// authoring-only (schema section 10): bindings pin bridge versions themselves, so it changes nothing queries read.
/// </summary>
public sealed class CurrentBridgeVersionUpdater(ITableGateway<BridgeVersionRecord> versions, ITableGateway<BridgeRecord> bridges)
    : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the updater joins: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Validation derives nothing here.</summary>
    /// <param name="versionId">The bridge version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Makes the newly published version its bridge's current version.</summary>
    /// <param name="versionId">The newly published bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>A task that completes when the pointer is written.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var bridge = await bridges.GetAsync(new { version.BridgeId }, cancellationToken);
        await bridges.UpdateAsync(bridge with { CurrentBridgeVersionId = versionId }, cancellationToken);
    }
}

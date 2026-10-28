using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Stores and removes whole bridges, analytical or report: a new bridge with its live version 1, published through the version
/// workflow so it is pinned by the active assumption set; and a bridge going away with its pins, versions and mappings, children first.
/// </summary>
public sealed class BridgeStore(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    BridgeDrafts drafts,
    VersionWorkflow workflow,
    AssumptionPins pins)
{
    /// <summary>Stores a bridge and publishes its version 1.</summary>
    /// <param name="bridge">The new bridge.</param>
    /// <param name="cardinality">Version 1's cardinality.</param>
    /// <param name="allocationMode">Version 1's allocation mode.</param>
    /// <param name="cloneFromVersionId">A version whose mappings version 1 starts with; null for none.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The bridge and its live version 1.</returns>
    public async Task<BridgeCreated> CreateAsync(
        BridgeRecord bridge,
        string cardinality,
        string allocationMode,
        long? cloneFromVersionId,
        CancellationToken cancellationToken)
    {
        await bridges.InsertAsync(bridge, cancellationToken);
        var draft = await drafts.CreateAsync(bridge.BridgeId, cardinality, allocationMode, cloneFromVersionId, cancellationToken);
        await workflow.PublishAsync(VersionKinds.BridgeVersion, draft.BridgeVersionId, cancellationToken);
        return new BridgeCreated { BridgeId = bridge.BridgeId, VersionId = draft.BridgeVersionId };
    }

    /// <summary>Deletes a bridge nothing needs any more: its pins, every version with its mappings, then the bridge.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the bridge is gone.</returns>
    public async Task RemoveAsync(long bridgeId, CancellationToken cancellationToken)
    {
        await pins.UnpinBridgeAsync(bridgeId, cancellationToken);
        await RemoveVersionsAsync([.. (await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken)).Select(version => version.BridgeVersionId)], cancellationToken);
        await bridges.DeleteAsync(new { BridgeId = bridgeId }, cancellationToken);
    }

    /// <summary>Deletes versions nothing references, their mappings first.</summary>
    /// <param name="versionIds">The bridge versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statement.</param>
    /// <returns>A task that completes when every row is deleted.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await mappings.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { BridgeVersionId = versionIds }, cancellationToken);
    }
}

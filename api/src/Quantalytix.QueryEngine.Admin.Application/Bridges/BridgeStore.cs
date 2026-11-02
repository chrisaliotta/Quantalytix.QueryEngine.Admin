using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Stores whole bridges, analytical or report: a new bridge with its live version, empty or with copies of another version's mappings.
/// Mappings name members by key, so they copy as they are, with their factors and weights, under new ids.
/// </summary>
public sealed class BridgeStore(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    LiveSnapshot live,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Stores a bridge with its live version.</summary>
    /// <param name="bridge">The new bridge.</param>
    /// <param name="cardinality">The version's cardinality.</param>
    /// <param name="allocationMode">The version's allocation mode, which the cardinality allows.</param>
    /// <param name="isEnabled">Whether the engine offers the bridge.</param>
    /// <param name="cloneFromVersionId">A version whose mappings the live version starts with; null for none.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The bridge and its live version.</returns>
    public async Task<BridgeCreated> CreateAsync(
        BridgeRecord bridge,
        string cardinality,
        string allocationMode,
        bool isEnabled,
        long? cloneFromVersionId,
        CancellationToken cancellationToken)
    {
        await bridges.InsertAsync(bridge, cancellationToken);
        var version = new BridgeVersionRecord
        {
            BridgeVersionId = ids.NextId(),
            BridgeId = bridge.BridgeId,
            SnapshotId = await live.IdAsync(cancellationToken),
            Cardinality = cardinality,
            AllocationMode = allocationMode,
            IsEnabled = isEnabled,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(version, cancellationToken);
        if (cloneFromVersionId is { } sourceVersionId) await CopyMappingsAsync(sourceVersionId, version.BridgeVersionId, cancellationToken);
        return new BridgeCreated { BridgeId = bridge.BridgeId, VersionId = version.BridgeVersionId };
    }

    /// <summary>Copies a version's mappings into another stored version under new ids.</summary>
    /// <param name="sourceVersionId">The version whose mappings are copied.</param>
    /// <param name="targetVersionId">The version that receives them, without mappings.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the copies are written.</returns>
    public async Task CopyMappingsAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        var copied = await mappings.ListAsync(new { BridgeVersionId = sourceVersionId }, cancellationToken);
        await mappings.InsertManyAsync([.. copied.Select(mapping => mapping with { BridgeMappingId = ids.NextId(), BridgeVersionId = targetVersionId })], cancellationToken);
    }
}

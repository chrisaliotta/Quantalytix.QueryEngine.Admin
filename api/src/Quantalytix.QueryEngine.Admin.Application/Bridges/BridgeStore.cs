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
        if (cloneFromVersionId is { } sourceVersionId)
            await CopyMappingsAsync(new Dictionary<long, long> { [sourceVersionId] = version.BridgeVersionId }, cancellationToken);
        return new BridgeCreated { BridgeId = bridge.BridgeId, VersionId = version.BridgeVersionId };
    }

    /// <summary>Copies versions' mappings into other stored versions under new ids, with one read and one write.</summary>
    /// <param name="targets">The version that receives each copied version's mappings; it has none yet.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the copies are written.</returns>
    public async Task CopyMappingsAsync(IReadOnlyDictionary<long, long> targets, CancellationToken cancellationToken)
    {
        var copied = await mappings.ListAsync(new { BridgeVersionId = targets.Keys.ToArray() }, cancellationToken);
        await mappings.InsertManyAsync([.. copied.Select(mapping => mapping with { BridgeMappingId = ids.NextId(), BridgeVersionId = targets[mapping.BridgeVersionId] })], cancellationToken);
    }
}

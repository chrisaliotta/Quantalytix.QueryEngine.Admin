using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Reads a bridge aggregate as stored: its live version for a change set, or any version for a read. What surrounds the version is
/// read in the version's own snapshot.
/// </summary>
public sealed class BridgeSnapshotLoader(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<DimensionRecord> dimensions,
    SnapshotDimensionReader dimensionKeys,
    MappingReferenceReader references,
    BridgeOwnerReader owners,
    LiveSnapshot live,
    SnapshotHolders holders)
{
    /// <summary>Reads a bridge with its live version (OI-27).</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<BridgeSnapshot> LoadLiveAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var bridge = await bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
        var all = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        return await LoadAsync(bridge, all.Single(version => version.SnapshotId == liveSnapshotId), all, cancellationToken);
    }

    /// <summary>Reads the bridge of a version with that version's settings and mappings.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<BridgeSnapshot> LoadVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var bridge = await bridges.GetAsync(new { version.BridgeId }, cancellationToken);
        var all = await versions.ListAsync(new { version.BridgeId }, cancellationToken);
        return await LoadAsync(bridge, version, all, cancellationToken);
    }

    /// <summary>Reads the version's mappings and what surrounds the bridge in the version's snapshot.</summary>
    private async Task<BridgeSnapshot> LoadAsync(BridgeRecord bridge, BridgeVersionRecord version, IReadOnlyList<BridgeVersionRecord> all, CancellationToken cancellationToken)
    {
        var allMappings = await mappings.ListAsync(new { BridgeVersionId = all.Select(row => row.BridgeVersionId).ToArray() }, cancellationToken);
        var bindings = await references.BindingsAsync(binding => binding.BridgeId == bridge.BridgeId, binding => binding.BridgeId!.Value, version.SnapshotId, cancellationToken);
        return new BridgeSnapshot
        {
            Bridge = bridge,
            Version = version,
            Mappings = [.. BridgeReadModels.Ordered(allMappings.Where(mapping => mapping.BridgeVersionId == version.BridgeVersionId))],
            Surroundings = new BridgeSurroundings
            {
                Source = await dimensions.GetAsync(new { DimensionId = bridge.SourceDimensionId }, cancellationToken),
                SourceKeys = await dimensionKeys.ReadAsync(bridge.SourceDimensionId, version.SnapshotId, cancellationToken),
                Target = await dimensions.GetAsync(new { DimensionId = bridge.TargetDimensionId }, cancellationToken),
                TargetKeys = await dimensionKeys.ReadAsync(bridge.TargetDimensionId, version.SnapshotId, cancellationToken),
                Owner = (await owners.OwnersAsync([bridge.BridgeId], version.SnapshotId, cancellationToken)).GetValueOrDefault(bridge.BridgeId),
                LiveSnapshotId = await live.IdAsync(cancellationToken),
                Versions = [.. all.OrderBy(row => row.CreatedAt)],
                FrozenSnapshotKeys = await holders.FrozenKeysAsync(all.Select(row => row.SnapshotId), cancellationToken),
                MappingCounts = all.ToDictionary(row => row.BridgeVersionId, row => allMappings.Count(mapping => mapping.BridgeVersionId == row.BridgeVersionId)),
                Bindings = [.. bindings[bridge.BridgeId]]
            }
        };
    }
}

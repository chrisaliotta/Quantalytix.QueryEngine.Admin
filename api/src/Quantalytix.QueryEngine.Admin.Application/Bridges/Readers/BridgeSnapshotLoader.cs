using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads a bridge aggregate as stored: its live version for a change set, or any version for a check.</summary>
public sealed class BridgeSnapshotLoader(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<AssumptionSetBridgeRecord> pins,
    PublishedDimensionReader published,
    MappingReferenceReader references,
    BridgeOwnerReader owners)
{
    /// <summary>Reads a bridge with its live version, the one every bridge has (OI-27).</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<BridgeSnapshot> LoadLiveAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var bridge = await bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
        var all = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        return await LoadAsync(bridge, all.Single(version => version.Status == PublicationStatuses.Published), all, cancellationToken);
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

    /// <summary>Reads the version's mappings and what surrounds the bridge.</summary>
    private async Task<BridgeSnapshot> LoadAsync(BridgeRecord bridge, BridgeVersionRecord version, IReadOnlyList<BridgeVersionRecord> all, CancellationToken cancellationToken)
    {
        var allMappings = await mappings.ListAsync(new { BridgeVersionId = all.Select(row => row.BridgeVersionId).ToArray() }, cancellationToken);
        var pinned = await pins.ListAsync(new { bridge.BridgeId }, cancellationToken);
        var bindings = await references.BindingsAsync(new { bridge.BridgeId }, binding => binding.BridgeId!.Value, cancellationToken);
        return new BridgeSnapshot
        {
            Bridge = bridge,
            Version = version,
            Mappings = [.. BridgeReadModels.Ordered(allMappings.Where(mapping => mapping.BridgeVersionId == version.BridgeVersionId))],
            Surroundings = new BridgeSurroundings
            {
                Source = await dimensions.GetAsync(new { DimensionId = bridge.SourceDimensionId }, cancellationToken),
                SourcePublished = await published.ReadAsync(bridge.SourceDimensionId, cancellationToken),
                Target = await dimensions.GetAsync(new { DimensionId = bridge.TargetDimensionId }, cancellationToken),
                TargetPublished = await published.ReadAsync(bridge.TargetDimensionId, cancellationToken),
                Owner = (await owners.OwnersAsync([bridge.BridgeId], cancellationToken)).GetValueOrDefault(bridge.BridgeId),
                Versions = [.. all.OrderBy(row => row.VersionNumber)],
                MappingCounts = all.ToDictionary(row => row.BridgeVersionId, row => allMappings.Count(mapping => mapping.BridgeVersionId == row.BridgeVersionId)),
                Bindings = [.. bindings[bridge.BridgeId]],
                Pins = await references.PinsAsync(
                    [.. pinned.Select(pin => (pin.AssumptionSetId, pin.BridgeVersionId))],
                    all.ToDictionary(row => row.BridgeVersionId, row => row.VersionNumber),
                    cancellationToken)
            }
        };
    }
}

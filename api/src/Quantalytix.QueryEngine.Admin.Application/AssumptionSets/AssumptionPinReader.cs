using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Reads a set's pins with each identity's key and each pinned version's number and status, as the client shows them.</summary>
public sealed class AssumptionPinReader(
    AssumptionPins pins,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions)
{
    /// <summary>Lists a set's pins: dimensions, mapping sets, then bridges, each by identity key.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The labelled pins.</returns>
    public async Task<IReadOnlyList<AssumptionPin>> ListAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var all = (await pins.ListAsync(assumptionSetId, cancellationToken)).ToLookup(pin => pin.Kind);
        return
        [
            .. await LabelAsync(all[VersionKinds.DimensionVersion],
                async ids => (await dimensions.ListAsync(new { DimensionId = ids }, cancellationToken)).ToDictionary(row => row.DimensionId, row => row.DimensionKey),
                async ids => (await dimensionVersions.ListAsync(new { DimensionVersionId = ids }, cancellationToken))
                    .ToDictionary(row => row.DimensionVersionId, row => (row.VersionNumber, row.Status))),
            .. await LabelAsync(all[VersionKinds.MappingVersion],
                async ids => (await mappingSets.ListAsync(new { MappingSetId = ids }, cancellationToken)).ToDictionary(row => row.MappingSetId, row => row.MappingSetKey),
                async ids => (await mappingVersions.ListAsync(new { MappingVersionId = ids }, cancellationToken))
                    .ToDictionary(row => row.MappingVersionId, row => (row.VersionNumber, row.Status))),
            .. await LabelAsync(all[VersionKinds.BridgeVersion],
                async ids => (await bridges.ListAsync(new { BridgeId = ids }, cancellationToken)).ToDictionary(row => row.BridgeId, row => row.BridgeKey),
                async ids => (await bridgeVersions.ListAsync(new { BridgeVersionId = ids }, cancellationToken))
                    .ToDictionary(row => row.BridgeVersionId, row => (row.VersionNumber, row.Status)))
        ];
    }

    /// <summary>Labels the pins of one kind with their identity keys and version numbers, ordered by key.</summary>
    private static async Task<IEnumerable<AssumptionPin>> LabelAsync(
        IEnumerable<PinnedVersion> pins,
        Func<long[], Task<Dictionary<long, string>>> keysOf,
        Func<long[], Task<Dictionary<long, (int Number, string Status)>>> versionsOf)
    {
        PinnedVersion[] kindPins = [.. pins];
        var keys = await keysOf([.. kindPins.Select(pin => pin.IdentityId)]);
        var versions = await versionsOf([.. kindPins.Select(pin => pin.VersionId)]);
        return kindPins
            .Select(pin => new AssumptionPin
            {
                Kind = pin.Kind,
                IdentityId = pin.IdentityId,
                IdentityKey = keys[pin.IdentityId],
                VersionId = pin.VersionId,
                VersionNumber = versions[pin.VersionId].Number,
                Status = versions[pin.VersionId].Status
            })
            .OrderBy(pin => pin.IdentityKey, StringComparer.Ordinal);
    }
}

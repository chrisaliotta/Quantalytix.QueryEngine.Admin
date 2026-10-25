using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Reads what the bridge mapping rules check: a bridge version, its mappings and the keys of the published versions of the
/// bridge's dimensions, which the active assumption set pins.
/// </summary>
public sealed class BridgeMappingSetReader(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeMappingRecord> mappings,
    PublishedDimensionReader dimensions)
{
    /// <summary>Reads a bridge version's mappings with the keys of its endpoint dimensions' published versions.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's mapping set.</returns>
    public async Task<BridgeMappingSet> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { BridgeVersionId = versionId }, cancellationToken);
        var bridge = await bridges.GetAsync(new { version.BridgeId }, cancellationToken);
        var rows = await mappings.ListAsync(new { BridgeVersionId = versionId }, cancellationToken);
        return new BridgeMappingSet
        {
            Version = version,
            Mappings = [.. rows.OrderBy(row => row.SourceMemberKey, StringComparer.Ordinal).ThenBy(row => row.TargetMemberKey, StringComparer.Ordinal)],
            Source = await dimensions.ReadAsync(bridge.SourceDimensionId, cancellationToken),
            Target = await dimensions.ReadAsync(bridge.TargetDimensionId, cancellationToken)
        };
    }
}

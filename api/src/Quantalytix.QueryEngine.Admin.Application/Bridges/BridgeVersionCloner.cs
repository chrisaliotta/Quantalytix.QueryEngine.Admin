using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Copies a bridge version's mappings into a new draft under new mapping IDs. Mappings name members by key, so they copy as
/// they are, with their factors and weights, and resolve in whichever dimension versions an assumption set pins.
/// </summary>
public sealed class BridgeVersionCloner(ITableGateway<BridgeMappingRecord> mappings, IIdGenerator ids)
{
    /// <summary>Copies the source's mappings into the target.</summary>
    /// <param name="sourceVersionId">The bridge version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the read and the insert.</param>
    /// <returns>A task that completes when the copies are written.</returns>
    public async Task CloneAsync(long sourceVersionId, BridgeVersionRecord target, CancellationToken cancellationToken)
    {
        var sourceMappings = await mappings.ListAsync(new { BridgeVersionId = sourceVersionId }, cancellationToken);
        await mappings.InsertManyAsync(
            [.. sourceMappings.Select(mapping => mapping with { BridgeMappingId = ids.NextId(), BridgeVersionId = target.BridgeVersionId })],
            cancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Creates a bridge's next draft, which the version workflow publishes at once: the empty version 1 of a new bridge, or the copy a
/// freeze continues in. Mappings name members by key, so they copy as they are, with their factors and weights, under new ids.
/// </summary>
public sealed class BridgeDrafts(
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Stores a draft with the bridge's next version number and the given settings, empty or with copies of another version's mappings.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cardinality">A <c>cardinality</c> value.</param>
    /// <param name="allocationMode">An <c>allocation_mode</c> value the cardinality allows.</param>
    /// <param name="cloneFromVersionId">The version whose mappings the draft copies; null for an empty draft.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The draft.</returns>
    public async Task<BridgeVersionRecord> CreateAsync(
        long bridgeId,
        string cardinality,
        string allocationMode,
        long? cloneFromVersionId,
        CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        var draft = new BridgeVersionRecord
        {
            BridgeVersionId = ids.NextId(),
            BridgeId = bridgeId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Cardinality = cardinality,
            AllocationMode = allocationMode,
            Status = PublicationStatuses.Draft,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(draft, cancellationToken);
        if (cloneFromVersionId is not { } sourceVersionId) return draft;
        var copied = await mappings.ListAsync(new { BridgeVersionId = sourceVersionId }, cancellationToken);
        await mappings.InsertManyAsync([.. copied.Select(mapping => mapping with { BridgeMappingId = ids.NextId(), BridgeVersionId = draft.BridgeVersionId })], cancellationToken);
        return draft;
    }
}

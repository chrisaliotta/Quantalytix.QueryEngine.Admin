using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Copies a mapping version's entries into a new draft under new entry IDs. Entries name members by key, so they copy as they
/// are and resolve in whichever dimension version an assumption set pins.
/// </summary>
public sealed class MappingVersionCloner(ITableGateway<DimensionMappingRecord> entries, IIdGenerator ids)
{
    /// <summary>Copies the source's entries into the target.</summary>
    /// <param name="sourceVersionId">The mapping version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the read and the insert.</param>
    /// <returns>A task that completes when the copies are written.</returns>
    public async Task CloneAsync(long sourceVersionId, MappingVersionRecord target, CancellationToken cancellationToken)
    {
        var sourceEntries = await entries.ListAsync(new { MappingVersionId = sourceVersionId }, cancellationToken);
        await entries.InsertManyAsync(
            [.. sourceEntries.Select(entry => entry with { DimensionMappingId = ids.NextId(), MappingVersionId = target.MappingVersionId })],
            cancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Creates a mapping set's next draft, which the version workflow publishes at once: the empty version 1 of a new mapping set, or
/// the copy a freeze continues in. Entries name members by key, so they copy as they are under new ids.
/// </summary>
public sealed class MappingDrafts(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Stores a draft with the mapping set's next version number, empty or with copies of another version's entries.</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cloneFromVersionId">The version whose entries the draft copies; null for an empty draft.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The draft.</returns>
    public async Task<MappingVersionRecord> CreateAsync(long mappingSetId, long? cloneFromVersionId, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        var draft = new MappingVersionRecord
        {
            MappingVersionId = ids.NextId(),
            MappingSetId = mappingSetId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Status = PublicationStatuses.Draft,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(draft, cancellationToken);
        if (cloneFromVersionId is not { } sourceVersionId) return draft;
        var copied = await entries.ListAsync(new { MappingVersionId = sourceVersionId }, cancellationToken);
        await entries.InsertManyAsync([.. copied.Select(entry => entry with { DimensionMappingId = ids.NextId(), MappingVersionId = draft.MappingVersionId })], cancellationToken);
        return draft;
    }
}

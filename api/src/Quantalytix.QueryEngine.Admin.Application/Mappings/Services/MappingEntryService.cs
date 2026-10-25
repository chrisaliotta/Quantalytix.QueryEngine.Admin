using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Reads and edits the entries of a mapping version: the source values and the keys of the members they map to.</summary>
public sealed class MappingEntryService(ITableGateway<DimensionMappingRecord> entries, IIdGenerator ids)
{
    /// <summary>Lists a version's entries.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entries by source key, in ordinal order.</returns>
    public async Task<IReadOnlyList<DimensionMappingRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        return BySourceKey(await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken));
    }

    /// <summary>Replaces every entry of a version with the given list, under new entry IDs.</summary>
    /// <param name="versionId">The draft or published mapping version.</param>
    /// <param name="input">The complete entry list.</param>
    /// <param name="cancellationToken">Cancels the delete and the insert.</param>
    /// <returns>The stored entries by source key.</returns>
    public async Task<IReadOnlyList<DimensionMappingRecord>> ReplaceAsync(long versionId, MappingEntriesInput input, CancellationToken cancellationToken)
    {
        List<DimensionMappingRecord> rows = [.. input.Entries.Select(entry => ToRecord(ids.NextId(), versionId, entry))];
        await entries.DeleteAsync(new { MappingVersionId = versionId }, cancellationToken);
        await entries.InsertManyAsync(rows, cancellationToken);
        return BySourceKey(rows);
    }

    /// <summary>Adds an entry to a version.</summary>
    /// <param name="versionId">The draft or published mapping version.</param>
    /// <param name="input">The new entry.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored entry.</returns>
    public async Task<DimensionMappingRecord> CreateAsync(long versionId, MappingEntryInput input, CancellationToken cancellationToken)
    {
        var entry = ToRecord(ids.NextId(), versionId, input);
        await entries.InsertAsync(entry, cancellationToken);
        return entry;
    }

    /// <summary>Replaces an entry's source value and member.</summary>
    /// <param name="reference">The draft or published mapping version and its entry.</param>
    /// <param name="input">The entry's new values.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored entry.</returns>
    public async Task<DimensionMappingRecord> UpdateAsync(MappingEntryReference reference, MappingEntryInput input, CancellationToken cancellationToken)
    {
        var entry = ToRecord(reference.EntryId, reference.VersionId, input);
        await entries.UpdateAsync(entry, cancellationToken);
        return entry;
    }

    /// <summary>Deletes an entry of a version.</summary>
    /// <param name="reference">The draft or published mapping version and its entry.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>A task that completes when the entry is deleted.</returns>
    public Task DeleteAsync(MappingEntryReference reference, CancellationToken cancellationToken)
    {
        return entries.DeleteAsync(new { DimensionMappingId = reference.EntryId }, cancellationToken);
    }

    /// <summary>The row of an entry of a version.</summary>
    private static DimensionMappingRecord ToRecord(long entryId, long versionId, MappingEntryInput input)
    {
        return new DimensionMappingRecord
        {
            DimensionMappingId = entryId,
            MappingVersionId = versionId,
            SourceKeyValue = input.SourceKeyValue,
            MemberKey = input.MemberKey
        };
    }

    /// <summary>Orders entries by source key, compared exactly.</summary>
    private static IReadOnlyList<DimensionMappingRecord> BySourceKey(IEnumerable<DimensionMappingRecord> rows)
    {
        return [.. rows.OrderBy(row => row.SourceKeyValue, StringComparer.Ordinal)];
    }
}

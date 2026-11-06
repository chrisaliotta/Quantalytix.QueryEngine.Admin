using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Copies a mapping version with its entries into a snapshot, for capture and restore. Entries name members by key, so they copy as
/// they are under new ids.
/// </summary>
/// <param name="versions">Reads the source and writes the copy's row.</param>
/// <param name="entries">Reads, clears and writes the entries.</param>
/// <param name="ids">Allocates the copies' ids.</param>
/// <param name="time">Stamps the copy.</param>
public sealed class MappingVersionCopier(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>mapping_set</c>.</summary>
    public string Kind => SnapshotKinds.MappingSet;

    /// <summary>Copies versions into a snapshot, each as a new version or over another version of its mapping set.</summary>
    /// <param name="copies">The versions to copy, each with the version whose entries it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var now = time.GetUtcNow().UtcDateTime;
        var sources = await versions.ListAsync(new { MappingVersionId = batch.SourceIds }, cancellationToken);
        await entries.DeleteAsync(new { MappingVersionId = batch.ReplacedIds }, cancellationToken);
        await versions.InsertManyAsync(
            [.. sources.Select(source => source with { MappingVersionId = batch.Targets[source.MappingVersionId], SnapshotId = targetSnapshotId, CreatedAt = now })],
            cancellationToken);

        var copied = await entries.ListAsync(new { MappingVersionId = batch.SourceIds }, cancellationToken);
        await entries.InsertManyAsync(
            [.. copied.Select(entry => entry with { DimensionMappingId = ids.NextId(), MappingVersionId = batch.Targets[entry.MappingVersionId] })],
            cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>The version's entries, each a source key value and the member key it maps to.</summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken);
        return ContentKey.Of(rows.Select(entry => entry with { DimensionMappingId = 0, MappingVersionId = 0 }));
    }
}

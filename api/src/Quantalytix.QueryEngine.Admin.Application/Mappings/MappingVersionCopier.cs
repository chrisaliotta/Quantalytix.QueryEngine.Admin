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

    /// <summary>Copies a version into a snapshot, as a new version or over another version of the mapping set.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">The version whose entries the copy replaces, or null for a new version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's id.</returns>
    public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var copy = source with { MappingVersionId = targetVersionId ?? ids.NextId(), SnapshotId = targetSnapshotId, CreatedAt = time.GetUtcNow().UtcDateTime };
        if (targetVersionId is { } target)
        {
            await entries.DeleteAsync(new { MappingVersionId = target }, cancellationToken);
            await versions.UpdateAsync(copy, cancellationToken);
        }
        else
        {
            await versions.InsertAsync(copy, cancellationToken);
        }

        var copied = await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken);
        await entries.InsertManyAsync([.. copied.Select(entry => entry with { DimensionMappingId = ids.NextId(), MappingVersionId = copy.MappingVersionId })], cancellationToken);
        return copy.MappingVersionId;
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

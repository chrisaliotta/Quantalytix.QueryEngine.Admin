using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Copies a dimension version, its range data type and content included, into a snapshot, for capture and restore.</summary>
/// <param name="versions">Reads the source and writes the copy's row.</param>
/// <param name="cloner">Copies the content.</param>
/// <param name="remover">Clears the content a copy replaces.</param>
/// <param name="ids">Allocates a new version's id.</param>
/// <param name="time">Stamps the copy.</param>
public sealed class DimensionVersionCopier(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionVersionCloner cloner,
    DimensionVersionRemover remover,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>dimension</c>.</summary>
    public string Kind => SnapshotKinds.Dimension;

    /// <summary>Copies a version into a snapshot, as a new version or over another version of the dimension.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">The version whose content the copy replaces, or null for a new version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's id.</returns>
    public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var copy = source with { DimensionVersionId = targetVersionId ?? ids.NextId(), SnapshotId = targetSnapshotId, CreatedAt = time.GetUtcNow().UtcDateTime };
        if (targetVersionId is { } target)
        {
            await remover.RemoveContentAsync([target], cancellationToken);
            await versions.UpdateAsync(copy, cancellationToken);
        }
        else
        {
            await versions.InsertAsync(copy, cancellationToken);
        }

        await cloner.CloneAsync(versionId, copy.DimensionVersionId, cancellationToken);
        return copy.DimensionVersionId;
    }
}

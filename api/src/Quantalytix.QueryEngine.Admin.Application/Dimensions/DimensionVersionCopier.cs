using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Copies a dimension version, its range data type and content included, into a snapshot, for capture and restore.</summary>
/// <param name="versions">Reads the source and writes the copy's row.</param>
/// <param name="levels">Reads the levels a content key names.</param>
/// <param name="members">Reads the members a content key names.</param>
/// <param name="bands">Reads the range bands a content key names.</param>
/// <param name="cloner">Copies the content.</param>
/// <param name="remover">Clears the content a copy replaces.</param>
/// <param name="ids">Allocates a new version's id.</param>
/// <param name="time">Stamps the copy.</param>
public sealed class DimensionVersionCopier(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    DimensionVersionCloner cloner,
    DimensionVersionRemover remover,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>dimension</c>.</summary>
    public string Kind => SnapshotKinds.Dimension;

    /// <summary>Copies versions into a snapshot, each as a new version or over another version of its dimension.</summary>
    /// <param name="copies">The versions to copy, each with the version whose content it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var now = time.GetUtcNow().UtcDateTime;
        var sources = await versions.ListAsync(new { DimensionVersionId = batch.SourceIds }, cancellationToken);
        await remover.RemoveContentAsync(batch.ReplacedIds, cancellationToken);
        await versions.InsertManyAsync(
            [.. sources.Select(source => source with { DimensionVersionId = batch.Targets[source.DimensionVersionId], SnapshotId = targetSnapshotId, CreatedAt = now })],
            cancellationToken);
        await cloner.CloneAsync(batch.Targets, cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>
    /// The version's range data type, its levels, its members by key with their parent's and level's keys, and its range bands by
    /// member key. The closure derives from the members, so it adds nothing.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var owner = new { DimensionVersionId = versionId };
        var version = await versions.GetAsync(owner, cancellationToken);
        var levelRows = await levels.ListAsync(owner, cancellationToken);
        var memberRows = await members.ListAsync(owner, cancellationToken);
        var levelKeys = levelRows.ToDictionary(level => level.DimensionLevelId, level => level.LevelKey);
        var memberKeys = memberRows.ToDictionary(member => member.DimensionMemberId, member => member.MemberKey);
        return ContentKey.Of(
            [version with { DimensionVersionId = 0, SnapshotId = 0, CreatedAt = default }],
            levelRows.Select(level => level with { DimensionLevelId = 0, DimensionVersionId = 0 }),
            memberRows.Select(member => new
            {
                Member = member with { DimensionMemberId = 0, DimensionVersionId = 0, ParentDimensionMemberId = null, DimensionLevelId = null },
                ParentKey = ContentKey.KeyOf(memberKeys, member.ParentDimensionMemberId),
                LevelKey = ContentKey.KeyOf(levelKeys, member.DimensionLevelId)
            }),
            (await bands.ListAsync(owner, cancellationToken)).Select(band => new
            {
                Band = band with { DimensionVersionId = 0, DimensionMemberId = 0 },
                MemberKey = memberKeys[band.DimensionMemberId]
            }));
    }
}

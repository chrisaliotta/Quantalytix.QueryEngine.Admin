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

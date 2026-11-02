using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads the keys of dimension versions, above all of a dimension's version in one snapshot: bindings, mapping entries, bridge
/// mappings and reports resolve the keys they name in their own snapshot's version of the dimension.
/// </summary>
public sealed class SnapshotDimensionReader(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Reads the keys of a dimension's version in a snapshot.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's keys, or null when the snapshot holds no version of the dimension.</returns>
    public async Task<DimensionKeys?> ReadAsync(long dimensionId, long snapshotId, CancellationToken cancellationToken)
    {
        var held = await versions.ListAsync(new { DimensionId = dimensionId, SnapshotId = snapshotId }, cancellationToken);
        return held.Count == 0 ? null : await KeysAsync(held[0], cancellationToken);
    }

    /// <summary>Reads the keys of a dimension version.</summary>
    /// <param name="version">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's keys.</returns>
    public async Task<DimensionKeys> KeysAsync(DimensionVersionRecord version, CancellationToken cancellationToken)
    {
        var owner = new { version.DimensionVersionId };
        var versionMembers = await members.ListAsync(owner, cancellationToken);
        var keyOf = versionMembers.ToDictionary(member => member.DimensionMemberId, member => member.MemberKey);
        return new DimensionKeys
        {
            Version = version,
            LevelKeys = (await levels.ListAsync(owner, cancellationToken)).Select(level => level.LevelKey).ToHashSet(StringComparer.Ordinal),
            MemberKeys = versionMembers.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            UnassignableMemberKeys = versionMembers.Where(member => !member.IsAssignable).Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            RootCount = versionMembers.Count(member => member.ParentDimensionMemberId is null),
            ParentKeys = versionMembers
                .Where(member => member.ParentDimensionMemberId is not null)
                .ToDictionary(member => member.MemberKey, member => keyOf[member.ParentDimensionMemberId!.Value], StringComparer.Ordinal)
        };
    }
}

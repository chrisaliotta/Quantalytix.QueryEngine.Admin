using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads a dimension aggregate as stored: its live version for a change set, or any version for a read. What surrounds the version is
/// read in the version's own snapshot.
/// </summary>
public sealed class DimensionSnapshotLoader(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    DimensionUsageReader usage,
    DimensionConsumers consumers,
    LiveSnapshot live,
    SnapshotHolders holders)
{
    /// <summary>Reads a dimension with its live version.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<DimensionSnapshot> LoadLiveAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
        var all = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        return await LoadAsync(dimension, all.Single(version => version.SnapshotId == liveSnapshotId), all, cancellationToken);
    }

    /// <summary>Reads the dimension of a version with that version's content.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<DimensionSnapshot> LoadVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var dimension = await dimensions.GetAsync(new { version.DimensionId }, cancellationToken);
        var all = await versions.ListAsync(new { version.DimensionId }, cancellationToken);
        return await LoadAsync(dimension, version, all, cancellationToken);
    }

    /// <summary>Reads the version's content, the dimension's roles and what surrounds them.</summary>
    private async Task<DimensionSnapshot> LoadAsync(
        DimensionRecord dimension,
        DimensionVersionRecord version,
        IReadOnlyList<DimensionVersionRecord> all,
        CancellationToken cancellationToken)
    {
        var tenantRoles = await roles.ListAsync(null, cancellationToken);
        var own = tenantRoles.Where(role => role.DimensionId == dimension.DimensionId).OrderBy(role => role.DimensionRoleKey, StringComparer.Ordinal).ToList();
        var allMembers = await members.ListAsync(new { DimensionVersionId = all.Select(row => row.DimensionVersionId).ToArray() }, cancellationToken);
        var memberCounts = all.ToDictionary(row => row.DimensionVersionId, row => allMembers.Count(member => member.DimensionVersionId == row.DimensionVersionId));
        return new DimensionSnapshot
        {
            Dimension = dimension,
            Profile = DimensionProfiles.Of(dimension.DimensionProfileKey),
            Roles = own,
            Version = version,
            Levels = [.. (await levels.ListAsync(new { version.DimensionVersionId }, cancellationToken)).OrderBy(level => level.Ordinal)],
            Members = [.. allMembers.Where(member => member.DimensionVersionId == version.DimensionVersionId).OrderBy(member => member.SortOrder).ThenBy(member => member.MemberKey, StringComparer.Ordinal)],
            Bands = [.. (await bands.ListAsync(new { version.DimensionVersionId }, cancellationToken)).OrderBy(band => band.Ordinal)],
            Surroundings = new DimensionSurroundings
            {
                Usage = await usage.ReadAsync(dimension.DimensionId, own, version.SnapshotId, cancellationToken),
                NamedKeys = await consumers.ListAsync(dimension.DimensionId, version.SnapshotId, cancellationToken),
                OtherRoleKeys = tenantRoles
                    .Where(role => role.DimensionId != dimension.DimensionId)
                    .Select(role => role.DimensionRoleKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                LiveSnapshotId = await live.IdAsync(cancellationToken),
                Versions = [.. all.OrderBy(row => row.CreatedAt)],
                FrozenSnapshotKeys = await holders.FrozenKeysAsync(all.Select(row => row.SnapshotId), cancellationToken),
                MemberCounts = memberCounts
            }
        };
    }
}

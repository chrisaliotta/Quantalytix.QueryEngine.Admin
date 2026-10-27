using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads a dimension aggregate as stored: its live version for a change set, or any version for a check.</summary>
public sealed class DimensionSnapshotLoader(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    DimensionUsageReader usage,
    DimensionConsumers consumers)
{
    /// <summary>Reads a dimension with its live version, the one every analytical dimension has.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<DimensionSnapshot> LoadLiveAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
        var all = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        return await LoadAsync(dimension, all.Single(version => version.Status == PublicationStatuses.Published), all, cancellationToken);
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
                Usage = await usage.ReadAsync(dimension.DimensionId, own, all, cancellationToken),
                NamedKeys = await consumers.ListAsync(dimension.DimensionId, cancellationToken),
                OtherRoleKeys = tenantRoles
                    .Where(role => role.DimensionId != dimension.DimensionId)
                    .Select(role => role.DimensionRoleKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                Versions = [.. all.OrderBy(row => row.VersionNumber)],
                MemberCounts = memberCounts
            }
        };
    }
}

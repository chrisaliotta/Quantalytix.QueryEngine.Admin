using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension manager's reads: lists, profiles, detail, version content, rule checks and delete checks. Report dimensions are
/// not this module's (M8): every read of one answers null, which the API answers 404.
/// </summary>
public sealed class DimensionQueries(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionMemberRecord> members,
    DimensionSnapshotLoader loader,
    DimensionRules rules,
    TimeProvider time)
{
    /// <summary>Lists the analytical dimensions of one origin; system dimensions only when the manager shows their profile.</summary>
    /// <param name="origin">A <c>definition_origin</c> value.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items by display name.</returns>
    public async Task<IReadOnlyList<DimensionListItem>> ListAsync(string origin, CancellationToken cancellationToken)
    {
        var listed = (await dimensions.ListAsync(new { DefinitionOrigin = origin, DimensionPurpose = DimensionPurposes.Analytical }, cancellationToken))
            .Where(dimension => DimensionProfiles.Of(dimension.DimensionProfileKey) is null or { Visibility: DimensionProfiles.ManagerVisibility })
            .ToList();
        var ids = listed.Select(dimension => dimension.DimensionId).ToArray();
        var rolesOf = (await roles.ListAsync(new { DimensionId = ids }, cancellationToken)).ToLookup(role => role.DimensionId);
        var versionsOf = (await versions.ListAsync(new { DimensionId = ids }, cancellationToken)).ToLookup(version => version.DimensionId);
        var counts = (await members.ListAsync(new { DimensionVersionId = versionsOf.SelectMany(group => group).Select(version => version.DimensionVersionId).ToArray() }, cancellationToken))
            .GroupBy(member => member.DimensionVersionId)
            .ToDictionary(group => group.Key, group => group.Count());
        return
        [
            .. listed
                .Select(dimension =>
                {
                    var own = versionsOf[dimension.DimensionId].ToList();
                    var memberCounts = own.ToDictionary(version => version.DimensionVersionId, version => counts.GetValueOrDefault(version.DimensionVersionId));
                    return DimensionReadModels.ListItem(dimension, rolesOf[dimension.DimensionId], own, memberCounts, TemporaryIds.PresentStored);
                })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>Lists the profiles with the dimension defined from each.</summary>
    /// <param name="available">True for only the profiles the manager offers: manager-visible and single-instance.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The profiles in registry order.</returns>
    public async Task<IReadOnlyList<AvailableDimensionProfile>> ProfilesAsync(bool available, CancellationToken cancellationToken)
    {
        var defined = (await dimensions.ListAsync(new { DefinitionOrigin = DefinitionOrigins.System }, cancellationToken))
            .Where(dimension => dimension.DimensionProfileKey is not null)
            .ToDictionary(dimension => dimension.DimensionProfileKey!, dimension => dimension.DimensionId, StringComparer.Ordinal);
        return
        [
            .. DimensionProfiles.All
                .Where(profile => !available || DimensionProfiles.IsOffered(profile))
                .Select(profile => new AvailableDimensionProfile { Profile = profile, DimensionId = defined.TryGetValue(profile.Key, out var id) ? id : null })
        ];
    }

    /// <summary>Reads a dimension's detail.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail, or null for a report dimension.</returns>
    public async Task<DimensionDetail?> DetailAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return await LiveAsync(dimensionId, cancellationToken) is { } snapshot ? DimensionReadModels.Detail(snapshot, TemporaryIds.PresentStored) : null;
    }

    /// <summary>Reads a version's content.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content, or null for a report dimension's version.</returns>
    public async Task<DimensionVersionContent?> ContentAsync(long versionId, CancellationToken cancellationToken)
    {
        return await VersionAsync(versionId, cancellationToken) is { } snapshot ? DimensionReadModels.Content(snapshot, TemporaryIds.PresentStored) : null;
    }

    /// <summary>Runs every dimension rule on a version and changes nothing.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report, or null for a report dimension's version.</returns>
    public async Task<ValidationReport?> CheckAsync(long versionId, CancellationToken cancellationToken)
    {
        if (await VersionAsync(versionId, cancellationToken) is not { } snapshot) return null;
        var checkedAt = time.GetUtcNow().UtcDateTime;
        return new ValidationReport { VersionId = versionId, CheckedAt = checkedAt, Violations = await rules.EvaluateAsync(snapshot, cancellationToken) };
    }

    /// <summary>Checks whether a dimension can be deleted.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check, or null for a report dimension.</returns>
    public async Task<DeleteCheck?> DimensionDeleteCheckAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return await LiveAsync(dimensionId, cancellationToken) is { } snapshot ? DimensionDeleteRules.Dimension(snapshot) : null;
    }

    /// <summary>Checks whether a version can be deleted.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check, or null for a report dimension's version.</returns>
    public async Task<DeleteCheck?> VersionDeleteCheckAsync(long versionId, CancellationToken cancellationToken)
    {
        return await VersionAsync(versionId, cancellationToken) is { } snapshot ? DimensionDeleteRules.Version(snapshot) : null;
    }

    /// <summary>Checks whether a member can be deleted.</summary>
    /// <param name="versionId">The member's version.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check, or null for a report dimension's member.</returns>
    public async Task<DeleteCheck?> MemberDeleteCheckAsync(long versionId, long memberId, CancellationToken cancellationToken)
    {
        if (await VersionAsync(versionId, cancellationToken) is not { } snapshot) return null;
        return DimensionDeleteRules.Member(snapshot, snapshot.Members.Single(member => member.DimensionMemberId == memberId));
    }

    /// <summary>Checks whether a level can be deleted.</summary>
    /// <param name="versionId">The level's version.</param>
    /// <param name="levelId">The level.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check, or null for a report dimension's level.</returns>
    public async Task<DeleteCheck?> LevelDeleteCheckAsync(long versionId, long levelId, CancellationToken cancellationToken)
    {
        if (await VersionAsync(versionId, cancellationToken) is not { } snapshot) return null;
        return DimensionDeleteRules.Level(snapshot, snapshot.Levels.Single(level => level.DimensionLevelId == levelId));
    }

    /// <summary>Checks whether a role can be deleted.</summary>
    /// <param name="roleId">The role.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check, or null for a report dimension's role.</returns>
    public async Task<DeleteCheck?> RoleDeleteCheckAsync(long roleId, CancellationToken cancellationToken)
    {
        var role = await roles.GetAsync(new { DimensionRoleId = roleId }, cancellationToken);
        return await LiveAsync(role.DimensionId, cancellationToken) is { } snapshot ? DimensionDeleteRules.Role(snapshot, role) : null;
    }

    /// <summary>The live snapshot of an analytical dimension, or null for a report dimension.</summary>
    private async Task<DimensionSnapshot?> LiveAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var dimension = await dimensions.GetAsync(new { DimensionId = dimensionId }, cancellationToken);
        return dimension.DimensionPurpose == DimensionPurposes.Report ? null : await loader.LoadLiveAsync(dimensionId, cancellationToken);
    }

    /// <summary>The snapshot of an analytical dimension's version, or null for a report dimension's.</summary>
    private async Task<DimensionSnapshot?> VersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadVersionAsync(versionId, cancellationToken);
        return snapshot.Dimension.DimensionPurpose == DimensionPurposes.Report ? null : snapshot;
    }
}

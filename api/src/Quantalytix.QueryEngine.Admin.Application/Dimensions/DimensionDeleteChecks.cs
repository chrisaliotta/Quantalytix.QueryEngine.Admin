using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Finds what blocks deleting a dimension, a dimension version, a level or a member: rows of other features that still
/// reference it, which must never be left dangling. Each check runs before anything is deleted.
/// </summary>
public sealed class DimensionDeleteChecks(
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<DimensionMappingRecord> mappingEntries,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<AssumptionSetDimensionRecord> assumptionSetDimensions,
    ITableGateway<AssumptionSetRecord> assumptionSets)
{
    /// <summary>
    /// What blocks deleting a dimension: it is not removable, a version is not a draft, or a binding, mapping set, bridge
    /// (either end) or assumption set references it.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="versions">Every version of the dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the dimension can be deleted.</returns>
    public async Task<string?> DimensionAsync(
        DimensionRecord dimension,
        IReadOnlyList<DimensionVersionRecord> versions,
        CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Dimension '{dimension.DimensionKey}'");
        if (!dimension.IsRemovable) blockers.Add("it is not removable");
        blockers.AddNamed(
            "it has non-draft versions",
            versions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        var boundBy = await bindings.ListAsync(new { dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by bindings", boundBy.Select(binding => $"'{binding.DisplayName}'"));
        var sets = await mappingSets.ListAsync(new { dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by mapping sets", sets.Select(set => $"'{set.MappingSetKey}'"));
        var bridgesFrom = await bridges.ListAsync(new { SourceDimensionId = dimension.DimensionId }, cancellationToken);
        var bridgesTo = await bridges.ListAsync(new { TargetDimensionId = dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by bridges", bridgesFrom.Concat(bridgesTo).Select(bridge => $"'{bridge.BridgeKey}'"));
        blockers.AddNamed("pinned by assumption sets", await AssumptionSetKeysAsync(new { dimension.DimensionId }, cancellationToken));
        return blockers.Detail();
    }

    /// <summary>
    /// What blocks deleting a draft dimension version: a mapping version that is not a draft, a bridge version (either end),
    /// a binding or an assumption set pins it. Draft mapping versions pinned to it are deleted with it.
    /// </summary>
    /// <param name="version">The draft dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the version can be deleted.</returns>
    public async Task<string?> VersionAsync(DimensionVersionRecord version, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Dimension version v{version.VersionNumber}");
        var mappings = await mappingVersions.ListAsync(new { version.DimensionVersionId }, cancellationToken);
        blockers.AddNamed(
            "pinned by non-draft mapping versions",
            await MappingVersionNamesAsync([.. mappings.Where(mapping => mapping.Status != PublicationStatuses.Draft)], cancellationToken));
        var bridgesFrom = await bridgeVersions.ListAsync(new { SourceDimensionVersionId = version.DimensionVersionId }, cancellationToken);
        var bridgesTo = await bridgeVersions.ListAsync(new { TargetDimensionVersionId = version.DimensionVersionId }, cancellationToken);
        blockers.AddNamed("pinned by bridge versions", await BridgeVersionNamesAsync([.. bridgesFrom, .. bridgesTo], cancellationToken));
        var boundBy = await bindings.ListAsync(new { PinnedDimensionVersionId = version.DimensionVersionId }, cancellationToken);
        blockers.AddNamed("pinned by bindings", boundBy.Select(binding => $"'{binding.DisplayName}'"));
        blockers.AddNamed("pinned by assumption sets", await AssumptionSetKeysAsync(new { version.DimensionVersionId }, cancellationToken));
        return blockers.Detail();
    }

    /// <summary>What blocks deleting a level: members that use it.</summary>
    /// <param name="level">The level of a draft version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The 409 detail naming the members, or null when the level can be deleted.</returns>
    public async Task<string?> LevelAsync(DimensionLevelRecord level, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Level '{level.LevelKey}'");
        var users = await members.ListAsync(new { level.LevelId }, cancellationToken);
        blockers.AddNamed("used by members", users.Select(member => $"'{member.MemberKey}'"));
        return blockers.Detail();
    }

    /// <summary>What blocks deleting a member: it has children, or a mapping entry, range band or bridge mapping references it.</summary>
    /// <param name="member">The member of a draft version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the member can be deleted.</returns>
    public async Task<string?> MemberAsync(DimensionMemberRecord member, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Member '{member.MemberKey}'");
        var children = await members.ListAsync(new { ParentMemberId = member.MemberId }, cancellationToken);
        blockers.AddNamed("it has child members", children.Select(child => $"'{child.MemberKey}'"));
        var entries = await mappingEntries.ListAsync(new { member.MemberId }, cancellationToken);
        blockers.AddNamed("mapped from source keys", entries.Select(entry => $"'{entry.SourceKeyValue}'"));
        var bandsOfMember = await bands.ListAsync(new { member.MemberId }, cancellationToken);
        blockers.AddNamed("used by range bands", bandsOfMember.Select(band => $"#{band.Ordinal}"));
        var mappedFrom = await bridgeMappings.ListAsync(new { SourceMemberId = member.MemberId }, cancellationToken);
        var mappedTo = await bridgeMappings.ListAsync(new { TargetMemberId = member.MemberId }, cancellationToken);
        long[] bridgeVersionIds = [.. mappedFrom.Concat(mappedTo).Select(mapping => mapping.BridgeVersionId).Distinct()];
        var usingVersions = await bridgeVersions.ListAsync(new { BridgeVersionId = bridgeVersionIds }, cancellationToken);
        blockers.AddNamed("used by the mappings of bridge versions", await BridgeVersionNamesAsync(usingVersions, cancellationToken));
        return blockers.Detail();
    }

    /// <summary>The keys of the assumption sets whose pins match a filter, quoted.</summary>
    private async Task<IEnumerable<string>> AssumptionSetKeysAsync(object pinFilter, CancellationToken cancellationToken)
    {
        var pins = await assumptionSetDimensions.ListAsync(pinFilter, cancellationToken);
        var sets = await assumptionSets.ListAsync(new { AssumptionSetId = pins.Select(pin => pin.AssumptionSetId).ToArray() }, cancellationToken);
        return sets.Select(set => $"'{set.AssumptionSetKey}'");
    }

    /// <summary>Mapping versions worded as their set's key, version number and status.</summary>
    private async Task<IEnumerable<string>> MappingVersionNamesAsync(IReadOnlyList<MappingVersionRecord> versions, CancellationToken cancellationToken)
    {
        var sets = await mappingSets.ListAsync(new { MappingSetId = versions.Select(version => version.MappingSetId).ToArray() }, cancellationToken);
        var keys = sets.ToDictionary(set => set.MappingSetId, set => set.MappingSetKey);
        return versions.Select(version => $"'{keys[version.MappingSetId]}' v{version.VersionNumber} ({version.Status})");
    }

    /// <summary>Bridge versions worded as their bridge's key and version number.</summary>
    private async Task<IEnumerable<string>> BridgeVersionNamesAsync(IReadOnlyList<BridgeVersionRecord> versions, CancellationToken cancellationToken)
    {
        var owners = await bridges.ListAsync(new { BridgeId = versions.Select(version => version.BridgeId).ToArray() }, cancellationToken);
        var keys = owners.ToDictionary(bridge => bridge.BridgeId, bridge => bridge.BridgeKey);
        return versions.Select(version => $"'{keys[version.BridgeId]}' v{version.VersionNumber}");
    }
}

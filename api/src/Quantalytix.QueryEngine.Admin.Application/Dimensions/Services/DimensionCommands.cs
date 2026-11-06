using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension manager's commands, which run on stored state, never inside a change set: create a user dimension, define a system
/// dimension from a profile, and delete a dimension. A new dimension starts with a live version, closure included; each command
/// advances the semantic revision.
/// </summary>
public sealed class DimensionCommands(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure,
    LiveDimensionVersions liveVersions,
    DimensionVersionRemover remover,
    DimensionSnapshotLoader loader,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>The title of the refusal of a key another dimension or role has.</summary>
    private const string KeyTakenTitle = "Key taken";

    /// <summary>Creates a user, analytical dimension with its first role and its live version (M2).</summary>
    /// <param name="input">The new dimension, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The dimension and its version, or a conflict when the dimension or role key is taken.</returns>
    public async Task<Outcome<DimensionCreated>> CreateAsync(CreateDimensionInput input, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> roleKeys = input.FirstRole is { } role ? [role.Key] : [];
        if (await TakenKeyAsync(input.Key, roleKeys, cancellationToken) is { } taken) return Outcome<DimensionCreated>.Refused(taken);

        var dimension = new DimensionRecord
        {
            DimensionId = ids.NextId(),
            DimensionKey = input.Key,
            DisplayName = input.DisplayName,
            DimensionKind = input.Kind,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = input.IsRemovable,
            IsEnabled = input.IsEnabled,
            DimensionPurpose = DimensionPurposes.Analytical
        };
        IReadOnlyList<(string Key, string DisplayName)> firstRole = input.FirstRole is { } first ? [(first.Key, first.DisplayName)] : [];
        return Outcome<DimensionCreated>.Success(await StoreAsync(dimension, firstRole, input.RangeDataType, [], cancellationToken));
    }

    /// <summary>Defines a profile's system dimension with the profile's required roles and members (M2, OI-16).</summary>
    /// <param name="profile">A profile the manager offers.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The dimension and its version, or a conflict when the profile's dimension exists or a key is taken.</returns>
    public async Task<Outcome<DimensionCreated>> DefineAsync(DimensionProfile profile, CancellationToken cancellationToken)
    {
        if ((await dimensions.ListAsync(new { DimensionProfileKey = profile.Key }, cancellationToken)).Count > 0)
            return Outcome<DimensionCreated>.Refused(Refusal.Conflict("Defined already", $"The {profile.DisplayName} dimension is defined already."));
        var key = string.Concat(profile.DisplayName.Where(character => !char.IsWhiteSpace(character)));
        if (await TakenKeyAsync(key, profile.RequiredRoleKeys, cancellationToken) is { } taken) return Outcome<DimensionCreated>.Refused(taken);

        var dimension = new DimensionRecord
        {
            DimensionId = ids.NextId(),
            DimensionKey = key,
            DisplayName = profile.DisplayName,
            DimensionKind = profile.Kind,
            DefinitionOrigin = DefinitionOrigins.System,
            IsRemovable = false,
            IsEnabled = true,
            DimensionPurpose = DimensionPurposes.Analytical,
            DimensionProfileKey = profile.Key
        };
        var named = profile.SeedNames ?? new Dictionary<string, string>();
        string NameOf(string seedKey) => named.GetValueOrDefault(seedKey, seedKey);
        var created = await StoreAsync(
            dimension,
            [.. profile.RequiredRoleKeys.Select(roleKey => (roleKey, NameOf(roleKey)))],
            null,
            [.. profile.RequiredMemberKeys.Select(memberKey => (memberKey, NameOf(memberKey)))],
            cancellationToken);
        return Outcome<DimensionCreated>.Success(created);
    }

    /// <summary>
    /// Deletes a dimension nothing blocks (M9): its live version with its content, its roles, then the row.
    /// </summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadLiveAsync(dimensionId, cancellationToken);
        if (snapshot.Dimension.DimensionPurpose == DimensionPurposes.Report) return DimensionRefusals.OwnedByReport(snapshot.Dimension);
        var check = DimensionDeleteRules.Dimension(snapshot);
        if (!check.Allowed) return Refusal.DeleteBlocked(check);

        await remover.RemoveVersionsAsync([snapshot.VersionId], cancellationToken);
        await remover.RemoveIdentitiesAsync([dimensionId], cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>The conflict of a dimension key another dimension has, or of a role key another role has, both ignoring case.</summary>
    private async Task<Refusal?> TakenKeyAsync(string dimensionKey, IReadOnlyList<string> roleKeys, CancellationToken cancellationToken)
    {
        var dimensionKeys = (await dimensions.ListAsync(null, cancellationToken)).Select(row => row.DimensionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (dimensionKeys.Contains(dimensionKey)) return Refusal.Conflict(KeyTakenTitle, $"Another dimension already uses the key '{dimensionKey}'.");
        var usedRoleKeys = (await roles.ListAsync(null, cancellationToken)).Select(row => row.DimensionRoleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roleKeys.FirstOrDefault(usedRoleKeys.Contains) is { } roleKey
            ? Refusal.Conflict(KeyTakenTitle, $"The role key '{roleKey}' is already used by another dimension.")
            : null;
    }

    /// <summary>Stores a new dimension with its roles and a live version holding its members and their closure.</summary>
    private async Task<DimensionCreated> StoreAsync(
        DimensionRecord dimension,
        IReadOnlyList<(string Key, string DisplayName)> roleSeeds,
        string? rangeDataType,
        IReadOnlyList<(string Key, string DisplayName)> memberSeeds,
        CancellationToken cancellationToken)
    {
        await dimensions.InsertAsync(dimension, cancellationToken);
        await roles.InsertManyAsync(
            [.. roleSeeds.Select(seed => new DimensionRoleRecord { DimensionRoleId = ids.NextId(), DimensionId = dimension.DimensionId, DimensionRoleKey = seed.Key, DisplayName = seed.DisplayName })],
            cancellationToken);
        var version = await liveVersions.CreateAsync(dimension.DimensionId, rangeDataType, cancellationToken);
        List<DimensionMemberRecord> seeded =
        [
            .. memberSeeds.Select((seed, index) => new DimensionMemberRecord
            {
                DimensionMemberId = ids.NextId(),
                DimensionVersionId = version.DimensionVersionId,
                MemberKey = seed.Key,
                DisplayName = seed.DisplayName,
                IsAssignable = true,
                SortOrder = index + 1
            })
        ];
        await members.InsertManyAsync(seeded, cancellationToken);
        await closure.InsertManyAsync(ClosureBuilder.ClosureOf(version.DimensionVersionId, seeded), cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return new DimensionCreated { DimensionId = dimension.DimensionId, VersionId = version.DimensionVersionId };
    }
}

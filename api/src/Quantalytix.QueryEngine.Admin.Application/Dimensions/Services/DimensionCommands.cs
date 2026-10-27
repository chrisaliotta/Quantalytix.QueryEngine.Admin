using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension manager's lifecycle commands, which run on stored state, never inside a change set: create a user dimension,
/// define a system dimension from a profile, delete a dimension or a frozen version, and freeze the live version (OI-14). A new
/// dimension's version 1 is published through the version workflow, so it is live, pinned by the active assumption set and its
/// closure built, as every later live version is.
/// </summary>
public sealed class DimensionCommands(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionMemberRecord> members,
    DimensionDrafts drafts,
    DimensionVersionRemover remover,
    DimensionSnapshotLoader loader,
    VersionWorkflow workflow,
    LivingVersionFreezer freezer,
    AssumptionPins pins,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>The title of the refusal of a key another dimension or role has.</summary>
    private const string KeyTakenTitle = "Key taken";

    /// <summary>Creates a user, analytical dimension with its first role and its live version 1 (M2).</summary>
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
    /// Deletes a dimension nothing blocks (M9): its active-set pin, its versions with their content, its roles, then the row.
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

        await pins.UnpinDimensionAsync(dimensionId, cancellationToken);
        await remover.RemoveAsync([.. snapshot.Surroundings.Versions.Select(version => version.DimensionVersionId)], cancellationToken);
        await roles.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
        await dimensions.DeleteAsync(new { DimensionId = dimensionId }, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>Deletes a version that is not live and that no historical assumption set pins, with its content.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadVersionAsync(versionId, cancellationToken);
        if (snapshot.Dimension.DimensionPurpose == DimensionPurposes.Report) return DimensionRefusals.OwnedByReport(snapshot.Dimension);
        var check = DimensionDeleteRules.Version(snapshot);
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        await remover.RemoveAsync([versionId], cancellationToken);
        return null;
    }

    /// <summary>
    /// Freezes the live version with a note and continues in a live clone (OI-14, OI-15); a locked profile refuses (M11).
    /// </summary>
    /// <param name="versionId">The live version.</param>
    /// <param name="note">Why it is frozen; blank is none.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The report and the new live version, or a 409 or 422 refusal with nothing changed.</returns>
    public async Task<Outcome<DimensionFreezeResult>> FreezeAsync(long versionId, string? note, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var dimension = await dimensions.GetAsync(new { version.DimensionId }, cancellationToken);
        if (dimension.DimensionPurpose == DimensionPurposes.Report) return Outcome<DimensionFreezeResult>.Refused(DimensionRefusals.OwnedByReport(dimension));
        if (DimensionProfiles.Of(dimension.DimensionProfileKey) is { EditPolicy: DimensionEditPolicies.Locked } locked)
            return Outcome<DimensionFreezeResult>.Refused(DimensionRefusals.ReadOnly(dimension, locked));

        var frozen = await freezer.FreezeAsync(VersionKinds.DimensionVersion, versionId, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), cancellationToken);
        if (!frozen.Succeeded) return Outcome<DimensionFreezeResult>.Refused(frozen.Refusal);
        var all = await versions.ListAsync(new { version.DimensionId }, cancellationToken);
        var live = all.Single(row => row.DimensionVersionId == frozen.Value.LiveVersionId);
        var memberCount = (await members.ListAsync(new { live.DimensionVersionId }, cancellationToken)).Count;
        return Outcome<DimensionFreezeResult>.Success(new DimensionFreezeResult
        {
            Report = frozen.Value.Report,
            Live = DimensionReadModels.Summary(live, memberCount, all, TemporaryIds.PresentStored)
        });
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

    /// <summary>Stores a new dimension with its roles and a draft version 1 holding its members, then publishes the draft.</summary>
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
        var draft = await drafts.CreateAsync(dimension.DimensionId, rangeDataType, cancellationToken);
        await members.InsertManyAsync(
            [
                .. memberSeeds.Select((seed, index) => new DimensionMemberRecord
                {
                    DimensionMemberId = ids.NextId(),
                    DimensionVersionId = draft.DimensionVersionId,
                    MemberKey = seed.Key,
                    DisplayName = seed.DisplayName,
                    IsAssignable = true,
                    SortOrder = index + 1
                })
            ],
            cancellationToken);
        await workflow.PublishAsync(VersionKinds.DimensionVersion, draft.DimensionVersionId, cancellationToken);
        return new DimensionCreated { DimensionId = dimension.DimensionId, VersionId = draft.DimensionVersionId };
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// What blocks deleting a dimension, a version, a member, a level or a role (M9, blueprint 01 "Delete checks"), decided on a
/// snapshot, so a delete-check endpoint and a change set's delete operation give the same answer. Only the live version's keys
/// are named by consumers; a profile's required keys block only under the <c>extend</c> policy (M11). The active assumption set's
/// pin goes with a deleted dimension; a historical set's pin blocks.
/// </summary>
public static class DimensionDeleteRules
{
    /// <summary>The phrase each usage kind blocks with.</summary>
    private static readonly IReadOnlyDictionary<string, string> Phrases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [DimensionUsageKinds.DatasetBinding] = "it is bound by dataset versions",
        [DimensionUsageKinds.MappingSet] = "it is referenced by mapping sets",
        [DimensionUsageKinds.Bridge] = "it is joined by bridges",
        [DimensionUsageKinds.CalculationDimension] = "it is grouped by calculation dimensions",
        [DimensionUsageKinds.ReportScenario] = "it is a scenario of reports",
        [DimensionUsageKinds.ReportSource] = "it is read by report sources",
        [DimensionUsageKinds.AssumptionSet] = "it is pinned by historical assumption sets"
    };

    /// <summary>What blocks deleting the snapshot's dimension: a system dimension, not removable, or referenced.</summary>
    /// <param name="snapshot">The dimension.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Dimension(DimensionSnapshot snapshot)
    {
        var blockers = new DeleteBlockers($"Dimension '{snapshot.Dimension.DisplayName}'");
        if (snapshot.Dimension.DefinitionOrigin == DefinitionOrigins.System) blockers.Add(DeleteBlockerCodes.SystemDimension, "it is a system dimension");
        if (!snapshot.Dimension.IsRemovable) blockers.Add(DeleteBlockerCodes.NotRemovable, "it is not removable");
        AddReferences(blockers, snapshot.Surroundings.Usage.Where(reference => reference.Kind != DimensionUsageKinds.AssumptionSet || reference.IsHistoricalPin));
        return blockers.Check();
    }

    /// <summary>What blocks deleting the snapshot's version: it is live, or a historical assumption set pins it.</summary>
    /// <param name="snapshot">The version.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Version(DimensionSnapshot snapshot)
    {
        var blockers = new DeleteBlockers($"Version {snapshot.Version.VersionNumber}");
        if (snapshot.IsLive) blockers.Add(DeleteBlockerCodes.LiveVersion, "it is the live version");
        AddReferences(blockers, snapshot.Surroundings.Usage.Where(reference => reference.IsHistoricalPin && reference.PinsVersion == snapshot.Version.VersionNumber));
        return blockers.Check();
    }

    /// <summary>What blocks deleting one member.</summary>
    /// <param name="snapshot">The member's version.</param>
    /// <param name="member">The member.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Member(DimensionSnapshot snapshot, DimensionMemberRecord member)
    {
        var blockers = new DeleteBlockers($"Member '{member.MemberKey}'");
        AddMember(blockers, snapshot, member, new HashSet<long> { member.DimensionMemberId }, "");
        return blockers.Check();
    }

    /// <summary>What blocks deleting several members together; a child among them does not block its parent.</summary>
    /// <param name="snapshot">The members' version.</param>
    /// <param name="removing">The members.</param>
    /// <returns>The check, each reason prefixed with its member's key.</returns>
    public static DeleteCheck Members(DimensionSnapshot snapshot, IReadOnlyCollection<DimensionMemberRecord> removing)
    {
        var blockers = new DeleteBlockers("The members");
        var ids = removing.Select(member => member.DimensionMemberId).ToHashSet();
        foreach (var member in removing) AddMember(blockers, snapshot, member, ids, $"'{member.MemberKey}': ");
        return blockers.Check();
    }

    /// <summary>What blocks deleting a level: members use it, or a consumer names its key in the live version.</summary>
    /// <param name="snapshot">The level's version.</param>
    /// <param name="level">The level.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Level(DimensionSnapshot snapshot, DimensionLevelRecord level)
    {
        var blockers = new DeleteBlockers($"Level '{level.LevelKey}'");
        blockers.AddNamed(
            DeleteBlockerCodes.ReferencedByMember,
            "it is used by members",
            snapshot.Members.Where(member => member.DimensionLevelId == level.DimensionLevelId).Select(member => $"'{member.MemberKey}'"));
        if (snapshot.IsLive)
            blockers.AddNamed(DeleteBlockerCodes.NamedByConsumers, "it is named by", Consumers(snapshot, isLevel: true, level.LevelKey));
        return blockers.Check();
    }

    /// <summary>What blocks deleting a role: the profile requires it, or something reads it.</summary>
    /// <param name="snapshot">The role's dimension.</param>
    /// <param name="role">The role.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Role(DimensionSnapshot snapshot, DimensionRoleRecord role)
    {
        var blockers = new DeleteBlockers($"Role '{role.DimensionRoleKey}'");
        if (RequiredBy(snapshot, snapshot.Profile?.RequiredRoleKeys, role.DimensionRoleKey))
            blockers.Add(DeleteBlockerCodes.RequiredByProfile, $"it is required by the {snapshot.Profile!.DisplayName} profile");
        AddReferences(blockers, snapshot.Surroundings.Usage.Where(reference => reference.RoleKey == role.DimensionRoleKey));
        return blockers.Check();
    }

    /// <summary>Whether a profile under the <c>extend</c> policy requires a key.</summary>
    /// <param name="snapshot">The dimension.</param>
    /// <param name="required">The profile's required member or role keys.</param>
    /// <param name="key">The key.</param>
    /// <returns>True when deleting the key is refused.</returns>
    public static bool RequiredBy(DimensionSnapshot snapshot, IReadOnlyList<string>? required, string key)
    {
        return snapshot.EditPolicy == DimensionEditPolicies.Extend && required!.Contains(key, StringComparer.Ordinal);
    }

    /// <summary>Adds what blocks deleting a member, each reason prefixed.</summary>
    private static void AddMember(DeleteBlockers blockers, DimensionSnapshot snapshot, DimensionMemberRecord member, IReadOnlySet<long> removing, string prefix)
    {
        if (RequiredBy(snapshot, snapshot.Profile?.RequiredMemberKeys, member.MemberKey))
            blockers.Add(DeleteBlockerCodes.RequiredByProfile, $"{prefix}it is required by the {snapshot.Profile!.DisplayName} profile");
        blockers.AddNamed(
            DeleteBlockerCodes.HasChildren,
            $"{prefix}it has child members",
            snapshot.Members
                .Where(child => child.ParentDimensionMemberId == member.DimensionMemberId && !removing.Contains(child.DimensionMemberId))
                .Select(child => $"'{child.MemberKey}'"));
        blockers.AddNamed(
            DeleteBlockerCodes.AssignedByBands,
            $"{prefix}it is assigned by range bands",
            snapshot.Bands.Where(band => band.DimensionMemberId == member.DimensionMemberId).Select(band => $"#{band.Ordinal}"));
        if (snapshot.IsLive)
            blockers.AddNamed(DeleteBlockerCodes.NamedByConsumers, $"{prefix}it is named by", Consumers(snapshot, isLevel: false, member.MemberKey));
    }

    /// <summary>Adds one <c>referenced_by_&lt;kind&gt;</c> reason per kind of reference.</summary>
    private static void AddReferences(DeleteBlockers blockers, IEnumerable<DimensionUsageReference> references)
    {
        foreach (var kind in references.GroupBy(reference => reference.Kind, StringComparer.Ordinal))
            blockers.AddNamed($"referenced_by_{kind.Key}", Phrases[kind.Key], kind.Select(reference => reference.Label ?? $"'{reference.Name}'"));
    }

    /// <summary>The live objects that name a level or member key.</summary>
    private static IEnumerable<string> Consumers(DimensionSnapshot snapshot, bool isLevel, string key)
    {
        return snapshot.Surroundings.NamedKeys.Where(reference => reference.IsLevel == isLevel && reference.Key == key).Select(reference => reference.Consumer);
    }
}

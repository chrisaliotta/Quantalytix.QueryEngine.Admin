using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// What blocks deleting a mapping set or one of its versions (B12, blueprint 01 "Delete checks"), decided on a snapshot so the delete
/// check and the delete give the same answer. The active assumption set's pin goes with a deleted mapping set; a historical set's pin
/// blocks.
/// </summary>
public static class MappingDeleteRules
{
    /// <summary>What blocks deleting the snapshot's mapping set: a dataset binding uses it, or a historical set pins a version.</summary>
    /// <param name="snapshot">The mapping set.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Set(MappingSetSnapshot snapshot)
    {
        var blockers = new DeleteBlockers($"Mapping set '{snapshot.Set.MappingSetKey}'");
        AddUsage(blockers, snapshot.Surroundings.Bindings, snapshot.Surroundings.Pins);
        return blockers.Check();
    }

    /// <summary>What blocks deleting the snapshot's version: it is live, or a historical set pins it.</summary>
    /// <param name="snapshot">The version.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Version(MappingSetSnapshot snapshot)
    {
        var blockers = new DeleteBlockers($"Version {snapshot.Version.VersionNumber}");
        AddVersion(blockers, snapshot.IsLive, snapshot.VersionId, snapshot.Surroundings.Pins);
        return blockers.Check();
    }

    /// <summary>Adds what blocks deleting a mapping set or bridge identity: the bindings that name it and the historical sets that pin it.</summary>
    /// <param name="blockers">The blockers to add to.</param>
    /// <param name="bindings">The bindings that name the identity.</param>
    /// <param name="pins">The assumption set pins of its versions.</param>
    public static void AddUsage(DeleteBlockers blockers, IEnumerable<MappingBindingUsage> bindings, IEnumerable<MappingAssumptionPin> pins)
    {
        blockers.AddNamed(
            DeleteBlockerCodes.ReferencedByDatasetBinding,
            "it is bound by dataset versions",
            bindings.Select(binding => $"'{binding.DatasetKey}' v{binding.DatasetVersionNumber} ({binding.RoleKey})"));
        blockers.AddNamed(
            DeleteBlockerCodes.ReferencedByAssumptionSet,
            "it is pinned by historical assumption sets",
            pins.Where(pin => !pin.IsActive).Select(pin => $"'{pin.AssumptionSetKey}'"));
    }

    /// <summary>Adds what blocks deleting a mapping or bridge version: it is live, or a historical set pins it.</summary>
    /// <param name="blockers">The blockers to add to.</param>
    /// <param name="isLive">Whether the version is live.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="pins">The assumption set pins of the identity's versions.</param>
    public static void AddVersion(DeleteBlockers blockers, bool isLive, long versionId, IEnumerable<MappingAssumptionPin> pins)
    {
        if (isLive) blockers.Add(DeleteBlockerCodes.LiveVersion, "it is the live version; only frozen versions can be deleted");
        blockers.AddNamed(
            DeleteBlockerCodes.ReferencedByAssumptionSet,
            "it is pinned by historical assumption sets",
            pins.Where(pin => !pin.IsActive && pin.VersionId == versionId).Select(pin => $"'{pin.AssumptionSetKey}'"));
    }
}

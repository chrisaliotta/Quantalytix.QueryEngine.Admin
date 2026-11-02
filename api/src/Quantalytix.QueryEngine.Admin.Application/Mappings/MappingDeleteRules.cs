using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// What blocks deleting a mapping set (B12, blueprint 01 "Delete checks"), decided on a snapshot so the delete check and the delete give
/// the same answer: a live dataset binding that names it, or a frozen snapshot that holds a version of it.
/// </summary>
public static class MappingDeleteRules
{
    /// <summary>What blocks deleting the snapshot's mapping set.</summary>
    /// <param name="snapshot">The mapping set with its live version.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Set(MappingSetSnapshot snapshot)
    {
        var blockers = new DeleteBlockers($"Mapping set '{snapshot.Set.MappingSetKey}'");
        AddUsage(blockers, snapshot.Surroundings.Bindings, snapshot.Surroundings.FrozenSnapshotKeys);
        return blockers.Check();
    }

    /// <summary>Adds what blocks deleting a mapping set or bridge identity: the bindings that name it and the frozen snapshots that hold it.</summary>
    /// <param name="blockers">The blockers to add to.</param>
    /// <param name="bindings">The live bindings that name the identity.</param>
    /// <param name="frozenSnapshotKeys">The keys of the frozen snapshots holding a version of it.</param>
    public static void AddUsage(DeleteBlockers blockers, IEnumerable<MappingBindingUsage> bindings, IEnumerable<string> frozenSnapshotKeys)
    {
        blockers.AddNamed(
            DeleteBlockerCodes.ReferencedByDatasetBinding,
            "it is bound by datasets",
            bindings.Select(binding => $"'{binding.DatasetKey}' ({binding.RoleKey})"));
        blockers.AddFrozenSnapshots(frozenSnapshotKeys);
    }
}

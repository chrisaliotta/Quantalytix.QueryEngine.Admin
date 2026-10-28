using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// What blocks deleting a bridge or one of its versions (B12, blueprint 01 "Delete checks"), decided on a snapshot so the delete check
/// and the delete give the same answer. A report bridge is never deleted here: it goes with its report source. The active assumption
/// set's pin goes with a deleted bridge; a historical set's pin blocks.
/// </summary>
public static class BridgeDeleteRules
{
    /// <summary>What blocks deleting the snapshot's bridge: a report owns it, a dataset binding uses it, or a historical set pins a version.</summary>
    /// <param name="snapshot">The bridge.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Bridge(BridgeSnapshot snapshot)
    {
        var blockers = Blockers($"Bridge '{snapshot.Bridge.BridgeKey}'", snapshot);
        MappingDeleteRules.AddUsage(blockers, snapshot.Surroundings.Bindings, snapshot.Surroundings.Pins);
        return blockers.Check();
    }

    /// <summary>What blocks deleting the snapshot's version: a report owns the bridge, the version is live, or a historical set pins it.</summary>
    /// <param name="snapshot">The version.</param>
    /// <returns>The check.</returns>
    public static DeleteCheck Version(BridgeSnapshot snapshot)
    {
        var blockers = Blockers($"Version {snapshot.Version.VersionNumber}", snapshot);
        MappingDeleteRules.AddVersion(blockers, snapshot.IsLive, snapshot.VersionId, snapshot.Surroundings.Pins);
        return blockers.Check();
    }

    /// <summary>The blockers of a subject, starting with the report that owns a report bridge.</summary>
    private static DeleteBlockers Blockers(string subject, BridgeSnapshot snapshot)
    {
        var blockers = new DeleteBlockers(subject);
        if (snapshot.Surroundings.Owner is { } owner)
            blockers.Add(DeleteBlockerCodes.OwnedByReport, $"it belongs to source '{owner.SourceKey}' of report '{owner.ReportKey}' and goes with the source");
        return blockers;
    }
}

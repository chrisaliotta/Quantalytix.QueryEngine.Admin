using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The level operations of the dimension change set: keys unique per version, ordinals kept 1..n top first, and a periodicity on
/// time dimensions only. A level that members use is not deleted (M9).
/// </summary>
internal static class DimensionLevelEdits
{
    /// <summary><c>saveLevel</c>; a new level goes to the bottom.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the level saved.</returns>
    public static DimensionSnapshot Save(DimensionSnapshot snapshot, DimensionSaveLevelOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var levelId = DimensionEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Levels.SingleOrDefault(level => level.DimensionLevelId == levelId);
        if (DimensionEditTargets.IsGone(input.Id, existing is not null, "Level", context)) return snapshot;
        var key = existing?.LevelKey ?? input.Key;
        if (existing is null && snapshot.Levels.Any(level => level.LevelKey == key))
        {
            context.Reject("input.key", $"Another level in this version uses '{key}'.");
            return snapshot;
        }

        var saved = new DimensionLevelRecord
        {
            DimensionLevelId = levelId,
            DimensionVersionId = snapshot.VersionId,
            LevelKey = key,
            DisplayName = input.DisplayName,
            Ordinal = existing?.Ordinal ?? snapshot.Levels.Count + 1,
            Periodicity = snapshot.Dimension.DimensionKind == DimensionKinds.Time ? input.Periodicity : null,
            IsLeafLevel = input.IsLeaf
        };
        return snapshot with { Levels = [.. snapshot.Levels.Where(level => level.DimensionLevelId != levelId), saved] };
    }

    /// <summary><c>deleteLevel</c>; the levels below move up.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the level.</returns>
    public static DimensionSnapshot Delete(DimensionSnapshot snapshot, DimensionDeleteLevelOperation operation, ChangeContext context)
    {
        if (DimensionEditTargets.Level(snapshot, operation.LevelId, context) is not { } level) return snapshot;
        var check = DimensionDeleteRules.Level(snapshot, level);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        return snapshot with { Levels = Numbered(snapshot.Levels.Where(row => row.DimensionLevelId != level.DimensionLevelId).OrderBy(row => row.Ordinal)) };
    }

    /// <summary><c>reorderLevels</c>: the ids name every level of the version once, top first.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the levels in the new order.</returns>
    public static DimensionSnapshot Reorder(DimensionSnapshot snapshot, DimensionReorderLevelsOperation operation, ChangeContext context)
    {
        var ordered = operation.OrderedIds.Select(context.Ids.Resolve).ToList();
        var byId = snapshot.Levels.ToDictionary(level => level.DimensionLevelId);
        if (ordered.Count != byId.Count || !ordered.ToHashSet().SetEquals(byId.Keys))
        {
            context.Reject("orderedIds", "List every level of the version exactly once.");
            return snapshot;
        }

        return snapshot with { Levels = Numbered(ordered.Select(id => byId[id])) };
    }

    /// <summary>Levels with ordinals 1..n in their order.</summary>
    private static List<DimensionLevelRecord> Numbered(IEnumerable<DimensionLevelRecord> levels)
    {
        return [.. levels.Select((level, index) => level with { Ordinal = index + 1 })];
    }
}

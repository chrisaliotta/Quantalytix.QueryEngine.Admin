using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The member operations of the dimension change set (M4): keys unique per version, parents and levels in the version, no
/// parent cycles, sibling sort orders kept 1..n, and profile properties valid on a system dimension. Only hierarchical members
/// have parents. A delete that something still needs is refused with its blockers (M9).
/// </summary>
internal static class DimensionMemberEdits
{
    /// <summary><c>saveMember</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the member saved.</returns>
    public static DimensionSnapshot Save(DimensionSnapshot snapshot, DimensionSaveMemberOperation operation, ChangeContext context)
    {
        var input = operation.Input;
        var memberId = DimensionEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Members.SingleOrDefault(member => member.DimensionMemberId == memberId);
        if (DimensionEditTargets.IsGone(input.Id, existing is not null, "Member", context)) return snapshot;

        var key = existing?.MemberKey ?? input.Key;
        if (existing is null && snapshot.Members.Any(member => member.MemberKey == key))
            context.Reject("input.key", $"Another member in this version uses '{key}'.");
        var parentId = ParentOf(snapshot, input.ParentId, memberId, "input.parentId", context);
        long? levelId = input.LevelId is { } level ? context.Ids.Resolve(level) : null;
        if (levelId is { } named && snapshot.Levels.All(row => row.DimensionLevelId != named))
            context.Reject("input.levelId", "Choose a level of this version.");
        if (snapshot.Profile is { } profile)
        {
            foreach (var (property, message) in DimensionPropertyRules.Problems(profile, input.Properties))
                context.Reject($"input.properties.{property}", message);
        }

        if (context.Refused) return snapshot;
        var stays = existing is not null && existing.ParentDimensionMemberId == parentId;
        var saved = new DimensionMemberRecord
        {
            DimensionMemberId = memberId,
            DimensionVersionId = snapshot.VersionId,
            MemberKey = key,
            DisplayName = input.DisplayName,
            Description = input.Description,
            ParentDimensionMemberId = parentId,
            DimensionLevelId = levelId,
            IsAssignable = input.IsAssignable,
            SortOrder = input.SortOrder ?? (stays ? existing!.SortOrder : NextSortOrder(snapshot.Members, parentId, memberId)),
            Properties = DimensionReadModels.PropertiesText(input.Properties)
        };
        var members = snapshot.Members.Where(member => member.DimensionMemberId != memberId).Append(saved).ToList();
        if (existing is not null && !stays) members = Renumber(members, existing.ParentDimensionMemberId);
        return snapshot with { Members = members };
    }

    /// <summary><c>deleteMember</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the member.</returns>
    public static DimensionSnapshot Delete(DimensionSnapshot snapshot, DimensionDeleteMemberOperation operation, ChangeContext context)
    {
        if (DimensionEditTargets.Member(snapshot, operation.MemberId, context) is not { } member) return snapshot;
        var check = DimensionDeleteRules.Member(snapshot, member);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        return snapshot with { Members = Renumber(snapshot.Members.Where(row => row.DimensionMemberId != member.DimensionMemberId), member.ParentDimensionMemberId) };
    }

    /// <summary><c>moveMember</c>: out of its siblings, into the new parent's at the index.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the member moved.</returns>
    public static DimensionSnapshot Move(DimensionSnapshot snapshot, DimensionMoveMemberOperation operation, ChangeContext context)
    {
        if (DimensionEditTargets.Member(snapshot, operation.MemberId, context) is not { } member) return snapshot;
        var parentId = ParentOf(snapshot, operation.To.ParentId, member.DimensionMemberId, "to.parentId", context);
        if (context.Refused) return snapshot;

        var others = Renumber(snapshot.Members.Where(row => row.DimensionMemberId != member.DimensionMemberId), member.ParentDimensionMemberId);
        var siblings = others.Where(row => row.ParentDimensionMemberId == parentId).OrderBy(row => row.SortOrder).ToList();
        siblings.Insert(Math.Min(operation.To.Index, siblings.Count), member with { ParentDimensionMemberId = parentId });
        var placed = siblings.Select((row, index) => row with { SortOrder = index + 1 }).ToList();
        return snapshot with { Members = [.. others.Where(row => row.ParentDimensionMemberId != parentId), .. placed] };
    }

    /// <summary>
    /// <c>importMembers</c>: <c>merge</c> upserts rows by key and keeps every other member; <c>replace</c> also removes the members
    /// the rows leave out, refused when one is still needed. A row's parent is another row or, merging, a member of the version.
    /// </summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the rows imported.</returns>
    public static DimensionSnapshot Import(DimensionSnapshot snapshot, DimensionImportMembersOperation operation, ChangeContext context)
    {
        var hierarchical = snapshot.Dimension.DimensionKind == DimensionKinds.Hierarchical;
        var replacing = operation.Mode == DimensionImportModes.Replace;
        var imported = operation.Rows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var stored = snapshot.Members.ToDictionary(member => member.MemberKey, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < operation.Rows.Count; index++)
        {
            var row = operation.Rows[index];
            if (!seen.Add(row.Key)) context.Reject($"rows[{index}].key", $"Key '{row.Key}' appears more than once.");
            else if (hierarchical && row.ParentKey is { } parentKey && (parentKey == row.Key || !(imported.Contains(parentKey) || (!replacing && stored.ContainsKey(parentKey)))))
                context.Reject($"rows[{index}].parentKey", $"Parent '{parentKey}' is not in the import{(replacing ? "" : " or this version")}.");
        }

        if (context.Refused) return snapshot;
        var removing = replacing ? snapshot.Members.Where(member => !imported.Contains(member.MemberKey)).ToList() : [];
        var check = DimensionDeleteRules.Members(snapshot, removing);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        var ids = stored.Where(entry => imported.Contains(entry.Key) || !replacing).ToDictionary(entry => entry.Key, entry => entry.Value.DimensionMemberId, StringComparer.Ordinal);
        foreach (var row in operation.Rows.Where(row => !ids.ContainsKey(row.Key))) ids[row.Key] = context.Ids.NewId();
        var parents = operation.Rows.ToDictionary(row => row.Key, row => hierarchical && row.ParentKey is { } parentKey ? ids[parentKey] : (long?)null, StringComparer.Ordinal);
        var kept = snapshot.Members.Where(member => !replacing || imported.Contains(member.MemberKey)).ToList();
        var after = kept.Select(member => parents.TryGetValue(member.MemberKey, out var parentId) ? member with { ParentDimensionMemberId = parentId } : member).ToList();
        if (FirstCycle(after.Concat(NewRows(snapshot, operation.Rows, ids, parents, stored))) is { } cycleKey)
        {
            context.Reject($"rows[{IndexOf(operation.Rows, cycleKey)}].parentKey", $"Parent of '{cycleKey}' would create a cycle.");
            return snapshot;
        }

        var renamed = after.Select(member => operation.Rows.FirstOrDefault(row => row.Key == member.MemberKey) is { } row
            ? member with { DisplayName = DisplayNameOf(row) }
            : member);
        List<DimensionMemberRecord> members = [.. renamed];
        foreach (var added in NewRows(snapshot, operation.Rows, ids, parents, stored))
            members.Add(added with { SortOrder = NextSortOrder(members, added.ParentDimensionMemberId, added.DimensionMemberId) });
        foreach (var parentId in members.Select(member => member.ParentDimensionMemberId).Distinct().ToList()) members = Renumber(members, parentId);
        return snapshot with { Members = members };
    }

    /// <summary><c>clearMembers</c>: removes every member, refused when one is still needed.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="context">Takes refusals.</param>
    /// <returns>The aggregate without members.</returns>
    public static DimensionSnapshot Clear(DimensionSnapshot snapshot, ChangeContext context)
    {
        var check = DimensionDeleteRules.Members(snapshot, snapshot.Members);
        if (check.Allowed) return snapshot with { Members = [] };
        context.Blocked(check);
        return snapshot;
    }

    /// <summary>
    /// The parent an input names for a member: none outside hierarchical dimensions; otherwise a member of the version that is
    /// not the member itself or below it.
    /// </summary>
    private static long? ParentOf(DimensionSnapshot snapshot, string? parent, long memberId, string path, ChangeContext context)
    {
        if (snapshot.Dimension.DimensionKind != DimensionKinds.Hierarchical || parent is null) return null;
        var parentId = context.Ids.Resolve(parent);
        if (snapshot.Members.All(member => member.DimensionMemberId != parentId)) context.Reject(path, "Choose a member in this version.");
        else if (parentId == memberId || Descendants(snapshot.Members, memberId).Contains(parentId)) context.Reject(path, "A member cannot sit under itself or its descendants.");
        return parentId;
    }

    /// <summary>Every member below a member.</summary>
    private static HashSet<long> Descendants(IReadOnlyList<DimensionMemberRecord> members, long memberId)
    {
        var children = members.ToLookup(member => member.ParentDimensionMemberId, member => member.DimensionMemberId);
        var found = new HashSet<long>();
        var pending = new Queue<long>([memberId]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var child in children[current])
            {
                if (found.Add(child)) pending.Enqueue(child);
            }
        }

        return found;
    }

    /// <summary>The members an import adds, without their sort order.</summary>
    private static IEnumerable<DimensionMemberRecord> NewRows(
        DimensionSnapshot snapshot,
        IReadOnlyList<DimensionMemberImportRow> rows,
        IReadOnlyDictionary<string, long> ids,
        IReadOnlyDictionary<string, long?> parents,
        IReadOnlyDictionary<string, DimensionMemberRecord> stored)
    {
        return rows.Where(row => !stored.ContainsKey(row.Key)).Select(row => new DimensionMemberRecord
        {
            DimensionMemberId = ids[row.Key],
            DimensionVersionId = snapshot.VersionId,
            MemberKey = row.Key,
            DisplayName = DisplayNameOf(row),
            ParentDimensionMemberId = parents[row.Key],
            IsAssignable = true,
            SortOrder = 0
        });
    }

    /// <summary>A row's display name; an empty one takes the key.</summary>
    private static string DisplayNameOf(DimensionMemberImportRow row)
    {
        return row.DisplayName.Length == 0 ? row.Key : row.DisplayName;
    }

    /// <summary>The key of a member on a parent cycle, or null when the parents form none.</summary>
    private static string? FirstCycle(IEnumerable<DimensionMemberRecord> members)
    {
        var byId = members.ToDictionary(member => member.DimensionMemberId);
        foreach (var start in byId.Values)
        {
            var visited = new HashSet<long>();
            for (long? current = start.DimensionMemberId; current is { } id && byId.ContainsKey(id); current = byId[id].ParentDimensionMemberId)
            {
                if (!visited.Add(id)) return byId[id].MemberKey;
            }
        }

        return null;
    }

    /// <summary>The index of the row with a key.</summary>
    private static int IndexOf(IReadOnlyList<DimensionMemberImportRow> rows, string key)
    {
        return Math.Max(0, rows.ToList().FindIndex(row => row.Key == key));
    }

    /// <summary>The sort order that appends a member to its siblings.</summary>
    private static int NextSortOrder(IEnumerable<DimensionMemberRecord> members, long? parentId, long memberId)
    {
        return members.Count(member => member.ParentDimensionMemberId == parentId && member.DimensionMemberId != memberId) + 1;
    }

    /// <summary>The members with one parent's children renumbered 1..n in their order.</summary>
    private static List<DimensionMemberRecord> Renumber(IEnumerable<DimensionMemberRecord> members, long? parentId)
    {
        var all = members.ToList();
        var siblings = all.Where(member => member.ParentDimensionMemberId == parentId).OrderBy(member => member.SortOrder).ThenBy(member => member.MemberKey, StringComparer.Ordinal).ToList();
        var numbered = siblings.Select((member, index) => (member.DimensionMemberId, SortOrder: index + 1)).ToDictionary(entry => entry.DimensionMemberId, entry => entry.SortOrder);
        return [.. all.Select(member => numbered.TryGetValue(member.DimensionMemberId, out var order) ? member with { SortOrder = order } : member)];
    }
}

using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The operations of the mapping set change set: the identity's display name and source namespace (key and dimension are fixed), and
/// the live version's entries. Whether entries comply is for the rules, which flag them in the preview and refuse the commit (B11).
/// </summary>
internal static class MappingEdits
{
    /// <summary>The kind of item the entry operations name.</summary>
    private const string Entry = "Entry";

    /// <summary><c>updateMappingSet</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The aggregate with the patched identity.</returns>
    public static MappingSetSnapshot Update(MappingSetSnapshot snapshot, MappingSetUpdateOperation operation)
    {
        var patch = operation.Patch;
        return snapshot with
        {
            Set = snapshot.Set with
            {
                DisplayName = patch.DisplayName ?? snapshot.Set.DisplayName,
                SourceNamespace = patch.SourceNamespace ?? snapshot.Set.SourceNamespace
            }
        };
    }

    /// <summary><c>saveEntry</c>: adds the entry, or replaces the entry with the same id.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the entry saved.</returns>
    public static MappingSetSnapshot Save(MappingSetSnapshot snapshot, MappingEntrySaveOperation operation, ChangeContext context)
    {
        if (!LiveEditTargets.AllPresent([operation.Input.Id], StoredIds(snapshot), Entry, context)) return snapshot;
        var saved = Row(snapshot, operation.Input, context);
        return snapshot with { Entries = [.. snapshot.Entries.Where(entry => entry.DimensionMappingId != saved.DimensionMappingId), saved] };
    }

    /// <summary><c>deleteEntry</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the entry.</returns>
    public static MappingSetSnapshot Delete(MappingSetSnapshot snapshot, MappingEntryDeleteOperation operation, ChangeContext context)
    {
        if (LiveEditTargets.Find(snapshot.Entries, entry => entry.DimensionMappingId, operation.EntryId, Entry, context) is not { } gone) return snapshot;
        return snapshot with { Entries = [.. snapshot.Entries.Where(entry => entry.DimensionMappingId != gone.DimensionMappingId)] };
    }

    /// <summary><c>replaceEntries</c>: the inputs become the version's entries; stored entries they leave out are removed.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the entries replaced.</returns>
    public static MappingSetSnapshot Replace(MappingSetSnapshot snapshot, MappingEntriesReplaceOperation operation, ChangeContext context)
    {
        if (!LiveEditTargets.AllPresent(operation.Entries.Select(input => input.Id), StoredIds(snapshot), Entry, context)) return snapshot;
        return snapshot with { Entries = [.. operation.Entries.Select(input => Row(snapshot, input, context))] };
    }

    /// <summary>The ids of the version's entries.</summary>
    private static HashSet<long> StoredIds(MappingSetSnapshot snapshot)
    {
        return [.. snapshot.Entries.Select(entry => entry.DimensionMappingId)];
    }

    /// <summary>The row of an entry input in the live version.</summary>
    private static DimensionMappingRecord Row(MappingSetSnapshot snapshot, MappingEntryInput input, ChangeContext context)
    {
        return new DimensionMappingRecord
        {
            DimensionMappingId = context.Ids.Resolve(input.Id),
            MappingVersionId = snapshot.VersionId,
            SourceKeyValue = input.SourceKeyValue,
            MemberKey = input.MemberKey
        };
    }
}

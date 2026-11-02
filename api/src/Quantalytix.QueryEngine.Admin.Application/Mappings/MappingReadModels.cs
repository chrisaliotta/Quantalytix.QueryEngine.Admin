using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Builds the mapping set read models from rows. Entry ids go through <c>presentId</c>, so a preview shows the entries its change
/// set created by their temporary ids and every other read shows stored ids.
/// </summary>
public static class MappingReadModels
{
    /// <summary>A dimension reference.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="held">Its version in the snapshot read; null when the snapshot holds none.</param>
    /// <returns>The reference.</returns>
    public static MappingDimensionRef DimensionRef(DimensionRecord dimension, DimensionVersionRecord? held)
    {
        return new MappingDimensionRef
        {
            Id = dimension.DimensionId,
            Key = dimension.DimensionKey,
            DisplayName = dimension.DisplayName,
            Kind = dimension.DimensionKind,
            Purpose = dimension.DimensionPurpose,
            VersionId = held?.DimensionVersionId
        };
    }

    /// <summary>A list item.</summary>
    /// <param name="set">The mapping set.</param>
    /// <param name="dimension">Its dimension.</param>
    /// <param name="versions">Its versions, the live one among them.</param>
    /// <param name="liveSnapshotId">The live snapshot.</param>
    /// <param name="entryCounts">The entry count of each version, by version id.</param>
    /// <param name="bindings">The live bindings that classify through it.</param>
    /// <returns>The list item.</returns>
    public static MappingSetListItem ListItem(
        MappingSetRecord set,
        MappingDimensionRef dimension,
        IEnumerable<MappingVersionRecord> versions,
        long liveSnapshotId,
        IReadOnlyDictionary<long, int> entryCounts,
        IReadOnlyList<MappingBindingUsage> bindings)
    {
        var ordered = versions.OrderByDescending(version => version.SnapshotId == liveSnapshotId).ThenByDescending(version => version.CreatedAt).ToList();
        return new MappingSetListItem
        {
            Id = set.MappingSetId,
            Key = set.MappingSetKey,
            DisplayName = set.DisplayName,
            SourceNamespace = set.SourceNamespace,
            DimensionId = set.DimensionId,
            Dimension = dimension,
            Versions = [.. ordered.Select(version => Summary(version, liveSnapshotId, entryCounts[version.MappingVersionId]))],
            LiveVersionId = ordered[0].MappingVersionId,
            LiveEntryCount = entryCounts[ordered[0].MappingVersionId],
            Bindings = bindings
        };
    }

    /// <summary>The list item of a snapshot's mapping set, with the snapshot's pending identity and entry count.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <returns>The list item.</returns>
    public static MappingSetListItem Detail(MappingSetSnapshot snapshot)
    {
        var surroundings = snapshot.Surroundings;
        var entryCounts = new Dictionary<long, int>(surroundings.EntryCounts) { [snapshot.VersionId] = snapshot.Entries.Count };
        var dimension = DimensionRef(surroundings.Dimension, surroundings.DimensionKeys?.Version);
        return ListItem(snapshot.Set, dimension, surroundings.Versions, surroundings.LiveSnapshotId, entryCounts, surroundings.Bindings);
    }

    /// <summary>The content of a snapshot's version.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="violations">The rule findings on the snapshot, as the rules name their items.</param>
    /// <param name="presentId">Writes each entry id.</param>
    /// <returns>The version with its entries, coverage and findings.</returns>
    public static MappingVersionContent Content(MappingSetSnapshot snapshot, IReadOnlyList<RuleViolation> violations, Func<long, string> presentId)
    {
        var version = snapshot.Version;
        return new MappingVersionContent
        {
            Version = new MappingVersion
            {
                Id = version.MappingVersionId,
                MappingSetId = version.MappingSetId,
                SnapshotId = version.SnapshotId,
                IsLive = snapshot.IsLive,
                CreatedAt = version.CreatedAt
            },
            Entries =
            [
                .. snapshot.Entries.OrderBy(entry => entry.SourceKeyValue, StringComparer.Ordinal).Select(entry => new MappingEntry
                {
                    Id = presentId(entry.DimensionMappingId),
                    VersionId = entry.MappingVersionId,
                    SourceKeyValue = entry.SourceKeyValue,
                    MemberKey = entry.MemberKey
                })
            ],
            Coverage = Coverage(snapshot),
            Violations = MappingViolations.Presented(violations, presentId)
        };
    }

    /// <summary>Everything the mapping set editor shows of a snapshot.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="violations">The rule findings on the snapshot.</param>
    /// <param name="presentId">Writes each entry id.</param>
    /// <returns>The edit state.</returns>
    public static MappingSetEditState EditState(MappingSetSnapshot snapshot, IReadOnlyList<RuleViolation> violations, Func<long, string> presentId)
    {
        return new MappingSetEditState { Set = Detail(snapshot), Content = Content(snapshot, violations, presentId) };
    }

    /// <summary>A version summary.</summary>
    /// <param name="version">The version.</param>
    /// <param name="liveSnapshotId">The live snapshot.</param>
    /// <param name="entryCount">Its entry count.</param>
    /// <returns>The summary.</returns>
    public static MappingVersionSummary Summary(MappingVersionRecord version, long liveSnapshotId, int entryCount)
    {
        return new MappingVersionSummary
        {
            Id = version.MappingVersionId,
            SnapshotId = version.SnapshotId,
            IsLive = version.SnapshotId == liveSnapshotId,
            CreatedAt = version.CreatedAt,
            EntryCount = entryCount
        };
    }

    /// <summary>The assignable members of the dimension's version and those no entry of the snapshot maps to.</summary>
    private static MappingCoverage Coverage(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.DimensionKeys is not { } keys) return new MappingCoverage { AssignableMemberCount = 0, UncoveredMemberKeys = [] };
        var assignable = keys.MemberKeys.Except(keys.UnassignableMemberKeys).ToList();
        var mapped = snapshot.Entries.Select(entry => entry.MemberKey).ToHashSet(StringComparer.Ordinal);
        return new MappingCoverage
        {
            AssignableMemberCount = assignable.Count,
            UncoveredMemberKeys = [.. assignable.Where(key => !mapped.Contains(key)).Order(StringComparer.Ordinal)]
        };
    }
}

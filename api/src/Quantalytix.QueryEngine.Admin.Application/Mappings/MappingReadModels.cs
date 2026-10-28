using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Builds the mapping set read models from rows. Entry ids go through <c>presentId</c>, so a preview shows the entries its change
/// set created by their temporary ids and every other read shows stored ids.
/// </summary>
public static class MappingReadModels
{
    /// <summary>A dimension reference.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="live">Its live version; null while it has none.</param>
    /// <returns>The reference.</returns>
    public static MappingDimensionRef DimensionRef(DimensionRecord dimension, DimensionVersionRecord? live)
    {
        return new MappingDimensionRef
        {
            Id = dimension.DimensionId,
            Key = dimension.DimensionKey,
            DisplayName = dimension.DisplayName,
            Kind = dimension.DimensionKind,
            Purpose = dimension.DimensionPurpose,
            PublishedVersionId = live?.DimensionVersionId,
            PublishedVersionNumber = live?.VersionNumber
        };
    }

    /// <summary>A list item.</summary>
    /// <param name="set">The mapping set.</param>
    /// <param name="dimension">Its dimension.</param>
    /// <param name="versions">Its versions.</param>
    /// <param name="entryCounts">The entry count of each version, by version id.</param>
    /// <param name="bindings">The bindings that classify through it.</param>
    /// <returns>The list item.</returns>
    public static MappingSetListItem ListItem(
        MappingSetRecord set,
        MappingDimensionRef dimension,
        IReadOnlyList<MappingVersionRecord> versions,
        IReadOnlyDictionary<long, int> entryCounts,
        IReadOnlyList<MappingBindingUsage> bindings)
    {
        var ordered = versions.OrderBy(version => version.VersionNumber).ToList();
        return new MappingSetListItem
        {
            Id = set.MappingSetId,
            Key = set.MappingSetKey,
            DisplayName = set.DisplayName,
            SourceNamespace = set.SourceNamespace,
            DimensionId = set.DimensionId,
            Dimension = dimension,
            Versions = [.. ordered.Select(version => Summary(version, entryCounts[version.MappingVersionId]))],
            PublishedEntryCount = entryCounts[ordered.Single(version => version.Status == PublicationStatuses.Published).MappingVersionId],
            Bindings = bindings
        };
    }

    /// <summary>The detail of a snapshot's mapping set, with the snapshot's pending identity and entry count.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <returns>The detail.</returns>
    public static MappingSetDetail Detail(MappingSetSnapshot snapshot)
    {
        var surroundings = snapshot.Surroundings;
        var entryCounts = new Dictionary<long, int>(surroundings.EntryCounts) { [snapshot.VersionId] = snapshot.Entries.Count };
        var dimension = DimensionRef(surroundings.Dimension, surroundings.Published?.Version);
        return new MappingSetDetail(ListItem(snapshot.Set, dimension, surroundings.Versions, entryCounts, surroundings.Bindings), surroundings.Pins);
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
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                CreatedAt = version.CreatedAt,
                PublishedAt = version.PublishedAt,
                RetiredAt = version.RetiredAt,
                FreezeNote = version.FreezeNote
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
    /// <param name="entryCount">Its entry count.</param>
    /// <returns>The summary.</returns>
    public static MappingVersionSummary Summary(MappingVersionRecord version, int entryCount)
    {
        return new MappingVersionSummary
        {
            Id = version.MappingVersionId,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            CreatedAt = version.CreatedAt,
            PublishedAt = version.PublishedAt,
            RetiredAt = version.RetiredAt,
            FreezeNote = version.FreezeNote,
            EntryCount = entryCount
        };
    }

    /// <summary>The assignable members of the dimension's live version and those no entry of the snapshot maps to.</summary>
    private static MappingCoverage Coverage(MappingSetSnapshot snapshot)
    {
        if (snapshot.Surroundings.Published is not { } published) return new MappingCoverage { AssignableMemberCount = 0, UncoveredMemberKeys = [] };
        var assignable = published.MemberKeys.Except(published.UnassignableMemberKeys).ToList();
        var mapped = snapshot.Entries.Select(entry => entry.MemberKey).ToHashSet(StringComparer.Ordinal);
        return new MappingCoverage
        {
            AssignableMemberCount = assignable.Count,
            UncoveredMemberKeys = [.. assignable.Where(key => !mapped.Contains(key)).Order(StringComparer.Ordinal)]
        };
    }
}

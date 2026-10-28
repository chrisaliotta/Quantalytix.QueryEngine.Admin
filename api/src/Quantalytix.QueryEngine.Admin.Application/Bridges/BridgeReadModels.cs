using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Builds the bridge read models from rows. Mapping ids go through <c>presentId</c>, so a preview shows the mappings its change set
/// created by their temporary ids and every other read shows stored ids.
/// </summary>
public static class BridgeReadModels
{
    /// <summary>A list item.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <param name="owner">The report source of a report bridge; null for an analytical one.</param>
    /// <param name="source">The source dimension.</param>
    /// <param name="target">The target dimension.</param>
    /// <param name="versions">The bridge's versions.</param>
    /// <param name="mappingCounts">The mapping count of each version, by version id.</param>
    /// <param name="bindings">The bindings that remap through it.</param>
    /// <returns>The list item.</returns>
    public static BridgeListItem ListItem(
        BridgeRecord bridge,
        BridgeOwner? owner,
        MappingDimensionRef source,
        MappingDimensionRef target,
        IReadOnlyList<BridgeVersionRecord> versions,
        IReadOnlyDictionary<long, int> mappingCounts,
        IReadOnlyList<MappingBindingUsage> bindings)
    {
        var ordered = versions.OrderBy(version => version.VersionNumber).ToList();
        return new BridgeListItem
        {
            Id = bridge.BridgeId,
            Key = bridge.BridgeKey,
            DisplayName = bridge.DisplayName,
            SourceDimensionId = bridge.SourceDimensionId,
            TargetDimensionId = bridge.TargetDimensionId,
            IsEnabled = bridge.IsEnabled,
            Owner = owner,
            Source = source,
            Target = target,
            Versions = [.. ordered.Select(version => Summary(version, mappingCounts[version.BridgeVersionId]))],
            PublishedMappingCount = mappingCounts[ordered.Single(version => version.Status == PublicationStatuses.Published).BridgeVersionId],
            Bindings = bindings
        };
    }

    /// <summary>The detail of a snapshot's bridge, with the snapshot's pending identity, settings and mapping count.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <returns>The detail.</returns>
    public static BridgeDetail Detail(BridgeSnapshot snapshot)
    {
        var surroundings = snapshot.Surroundings;
        var versions = surroundings.Versions.Select(version => version.BridgeVersionId == snapshot.VersionId ? snapshot.Version : version).ToList();
        var mappingCounts = new Dictionary<long, int>(surroundings.MappingCounts) { [snapshot.VersionId] = snapshot.Mappings.Count };
        var item = ListItem(
            snapshot.Bridge,
            surroundings.Owner,
            MappingReadModels.DimensionRef(surroundings.Source, surroundings.SourcePublished?.Version),
            MappingReadModels.DimensionRef(surroundings.Target, surroundings.TargetPublished?.Version),
            versions,
            mappingCounts,
            surroundings.Bindings);
        return new BridgeDetail(item, surroundings.Pins);
    }

    /// <summary>The content of a snapshot's version.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="violations">The rule findings on the snapshot, as the rules name their items.</param>
    /// <param name="presentId">Writes each mapping id.</param>
    /// <returns>The version with its mappings, allocation summary and findings.</returns>
    public static BridgeVersionContent Content(BridgeSnapshot snapshot, IReadOnlyList<RuleViolation> violations, Func<long, string> presentId)
    {
        var version = snapshot.Version;
        return new BridgeVersionContent
        {
            Version = new BridgeVersion
            {
                Id = version.BridgeVersionId,
                BridgeId = version.BridgeId,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                Cardinality = version.Cardinality,
                AllocationMode = version.AllocationMode,
                CreatedAt = version.CreatedAt,
                PublishedAt = version.PublishedAt,
                RetiredAt = version.RetiredAt,
                FreezeNote = version.FreezeNote
            },
            Mappings =
            [
                .. Ordered(snapshot.Mappings).Select(mapping => new BridgeMapping
                {
                    Id = presentId(mapping.BridgeMappingId),
                    VersionId = mapping.BridgeVersionId,
                    SourceMemberKey = mapping.SourceMemberKey,
                    TargetMemberKey = mapping.TargetMemberKey,
                    Factor = mapping.Factor,
                    AllocationWeight = mapping.AllocationWeight,
                    SourceSelection = mapping.SourceSelection
                })
            ],
            Allocation = new BridgeAllocationSummary
            {
                Sources =
                [
                    .. snapshot.Mappings
                        .GroupBy(mapping => mapping.SourceMemberKey, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new BridgeSourceAllocation
                        {
                            SourceMemberKey = group.Key,
                            TargetCount = group.Count(),
                            WeightSum = group.Sum(mapping => mapping.AllocationWeight)
                        })
                ]
            },
            Violations = MappingViolations.Presented(violations, presentId)
        };
    }

    /// <summary>Everything the bridge editor shows of a snapshot.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="violations">The rule findings on the snapshot.</param>
    /// <param name="presentId">Writes each mapping id.</param>
    /// <returns>The edit state.</returns>
    public static BridgeEditState EditState(BridgeSnapshot snapshot, IReadOnlyList<RuleViolation> violations, Func<long, string> presentId)
    {
        return new BridgeEditState { Bridge = Detail(snapshot), Content = Content(snapshot, violations, presentId) };
    }

    /// <summary>A version summary.</summary>
    /// <param name="version">The version.</param>
    /// <param name="mappingCount">Its mapping count.</param>
    /// <returns>The summary.</returns>
    public static BridgeVersionSummary Summary(BridgeVersionRecord version, int mappingCount)
    {
        return new BridgeVersionSummary
        {
            Id = version.BridgeVersionId,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            Cardinality = version.Cardinality,
            AllocationMode = version.AllocationMode,
            CreatedAt = version.CreatedAt,
            PublishedAt = version.PublishedAt,
            RetiredAt = version.RetiredAt,
            FreezeNote = version.FreezeNote,
            MappingCount = mappingCount
        };
    }

    /// <summary>Mappings by source member key, then target member key, compared exactly.</summary>
    /// <param name="mappings">The mappings.</param>
    /// <returns>The mappings in order.</returns>
    public static IEnumerable<BridgeMappingRecord> Ordered(IEnumerable<BridgeMappingRecord> mappings)
    {
        return mappings.OrderBy(mapping => mapping.SourceMemberKey, StringComparer.Ordinal).ThenBy(mapping => mapping.TargetMemberKey, StringComparer.Ordinal);
    }
}

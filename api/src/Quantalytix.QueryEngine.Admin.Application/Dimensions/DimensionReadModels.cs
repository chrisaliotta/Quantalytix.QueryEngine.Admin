using System.Text.Json.Nodes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Builds the contract's read models from rows. Every id goes through <c>presentId</c>, so a preview shows the items its change set
/// created by their temporary ids and every other read shows stored ids.
/// </summary>
public static class DimensionReadModels
{
    /// <summary>A list item.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="roles">Its roles.</param>
    /// <param name="versions">Its versions.</param>
    /// <param name="memberCounts">The member count of each version, by version id.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The list item.</returns>
    public static DimensionListItem ListItem(
        DimensionRecord dimension,
        IEnumerable<DimensionRoleRecord> roles,
        IReadOnlyList<DimensionVersionRecord> versions,
        IReadOnlyDictionary<long, int> memberCounts,
        Func<long, string> presentId)
    {
        var ordered = versions.OrderBy(version => version.VersionNumber).ToList();
        var live = ordered.SingleOrDefault(version => version.Status == PublicationStatuses.Published);
        return new DimensionListItem
        {
            Id = presentId(dimension.DimensionId),
            Key = dimension.DimensionKey,
            DisplayName = dimension.DisplayName,
            Kind = dimension.DimensionKind,
            Origin = dimension.DefinitionOrigin,
            Purpose = dimension.DimensionPurpose,
            OwnerReport = null,
            ProfileKey = dimension.DimensionProfileKey,
            IsEnabled = dimension.IsEnabled,
            IsRemovable = dimension.IsRemovable,
            Profile = DimensionProfiles.Of(dimension.DimensionProfileKey),
            Roles = [.. roles.OrderBy(role => role.DimensionRoleKey, StringComparer.Ordinal).Select(role => Role(role, presentId))],
            Versions = [.. ordered.Select(version => Summary(version, memberCounts[version.DimensionVersionId], ordered, presentId))],
            PublishedMemberCount = live is null ? null : memberCounts[live.DimensionVersionId]
        };
    }

    /// <summary>The detail of a snapshot's dimension, with the snapshot's pending roles and version in place of the stored ones.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The detail.</returns>
    public static DimensionDetail Detail(DimensionSnapshot snapshot, Func<long, string> presentId)
    {
        var versions = snapshot.Surroundings.Versions.Select(version => version.DimensionVersionId == snapshot.VersionId ? snapshot.Version : version).ToList();
        var memberCounts = new Dictionary<long, int>(snapshot.Surroundings.MemberCounts) { [snapshot.VersionId] = snapshot.Members.Count };
        return new DimensionDetail(ListItem(snapshot.Dimension, snapshot.Roles, versions, memberCounts, presentId), snapshot.Surroundings.Usage);
    }

    /// <summary>The content of a snapshot's version.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The version with its levels, members and bands.</returns>
    public static DimensionVersionContent Content(DimensionSnapshot snapshot, Func<long, string> presentId)
    {
        var rangeDataType = snapshot.Version.RangeDataType;
        return new DimensionVersionContent
        {
            Version = Version(snapshot.Version, snapshot.Surroundings.Versions, presentId),
            Levels = [.. snapshot.Levels.OrderBy(level => level.Ordinal).Select(level => Level(level, presentId))],
            Members = [.. snapshot.Members.OrderBy(member => member.SortOrder).ThenBy(member => member.MemberKey, StringComparer.Ordinal).Select(member => Member(member, presentId))],
            Bands =
            [
                .. snapshot.Bands.OrderBy(band => band.Ordinal).Select(band =>
                {
                    var (lower, upper) = DimensionBandBounds.Of(band, rangeDataType);
                    return new DimensionRangeBand
                    {
                        Ordinal = band.Ordinal,
                        MemberId = presentId(band.DimensionMemberId),
                        Lower = lower,
                        Upper = upper,
                        LowerInclusive = band.LowerInclusive,
                        UpperInclusive = band.UpperInclusive
                    };
                })
            ]
        };
    }

    /// <summary>Everything the dimension editor shows of a snapshot.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The edit state.</returns>
    public static DimensionEditState EditState(DimensionSnapshot snapshot, Func<long, string> presentId)
    {
        return new DimensionEditState { Dimension = Detail(snapshot, presentId), Content = Content(snapshot, presentId) };
    }

    /// <summary>A version summary.</summary>
    /// <param name="version">The version.</param>
    /// <param name="memberCount">Its member count.</param>
    /// <param name="versions">Every version of its dimension, which derives <c>supersededBy</c>.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>The summary.</returns>
    public static DimensionVersionSummary Summary(
        DimensionVersionRecord version,
        int memberCount,
        IReadOnlyList<DimensionVersionRecord> versions,
        Func<long, string> presentId)
    {
        return new DimensionVersionSummary
        {
            Id = presentId(version.DimensionVersionId),
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            PublishedAt = version.PublishedAt,
            RetiredAt = version.RetiredAt,
            SupersededBy = SupersededBy(version, versions),
            FreezeNote = version.FreezeNote,
            MemberCount = memberCount
        };
    }

    /// <summary>The member properties a row stores, as an object; none stored is an empty object.</summary>
    /// <param name="properties">The <c>properties</c> JSON text.</param>
    /// <returns>The properties.</returns>
    public static JsonObject PropertiesOf(string? properties)
    {
        return properties is null ? [] : JsonNode.Parse(properties)!.AsObject();
    }

    /// <summary>The <c>properties</c> JSON text of member properties; none is null.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>The JSON text, or null.</returns>
    public static string? PropertiesText(JsonObject properties)
    {
        return properties.Count == 0 ? null : properties.ToJsonString();
    }

    /// <summary>A version.</summary>
    private static DimensionVersion Version(DimensionVersionRecord version, IReadOnlyList<DimensionVersionRecord> versions, Func<long, string> presentId)
    {
        return new DimensionVersion
        {
            Id = presentId(version.DimensionVersionId),
            DimensionId = presentId(version.DimensionId),
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            RangeDataType = version.RangeDataType,
            CreatedAt = version.CreatedAt,
            PublishedAt = version.PublishedAt,
            RetiredAt = version.RetiredAt,
            SupersededBy = SupersededBy(version, versions),
            FreezeNote = version.FreezeNote
        };
    }

    /// <summary>The next version number of a frozen version; null for any other.</summary>
    private static int? SupersededBy(DimensionVersionRecord version, IReadOnlyList<DimensionVersionRecord> versions)
    {
        if (version.Status != PublicationStatuses.Retired) return null;
        return versions.Where(other => other.VersionNumber > version.VersionNumber).Select(other => (int?)other.VersionNumber).Min();
    }

    /// <summary>A role.</summary>
    private static DimensionRole Role(DimensionRoleRecord role, Func<long, string> presentId)
    {
        return new DimensionRole
        {
            Id = presentId(role.DimensionRoleId),
            DimensionId = presentId(role.DimensionId),
            Key = role.DimensionRoleKey,
            DisplayName = role.DisplayName,
            Description = role.Description
        };
    }

    /// <summary>A level.</summary>
    private static DimensionLevel Level(DimensionLevelRecord level, Func<long, string> presentId)
    {
        return new DimensionLevel
        {
            Id = presentId(level.DimensionLevelId),
            VersionId = presentId(level.DimensionVersionId),
            Key = level.LevelKey,
            DisplayName = level.DisplayName,
            Ordinal = level.Ordinal,
            IsLeaf = level.IsLeafLevel,
            Periodicity = level.Periodicity
        };
    }

    /// <summary>A member.</summary>
    private static DimensionMember Member(DimensionMemberRecord member, Func<long, string> presentId)
    {
        return new DimensionMember
        {
            Id = presentId(member.DimensionMemberId),
            VersionId = presentId(member.DimensionVersionId),
            Key = member.MemberKey,
            DisplayName = member.DisplayName,
            Description = member.Description,
            ParentId = member.ParentDimensionMemberId is { } parentId ? presentId(parentId) : null,
            LevelId = member.DimensionLevelId is { } levelId ? presentId(levelId) : null,
            IsAssignable = member.IsAssignable,
            SortOrder = member.SortOrder,
            Properties = PropertiesOf(member.Properties)
        };
    }
}

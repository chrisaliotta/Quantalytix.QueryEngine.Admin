using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The dimension aggregate in memory: the identity row, its roles and one version's content (the live version for a change
/// set), with what surrounds it as stored. Operations, rules, delete checks and read models all work on these values.
/// </summary>
public sealed record DimensionSnapshot : IVersionSnapshot
{
    /// <summary>The dimension.</summary>
    public required DimensionRecord Dimension { get; init; }

    /// <summary>The dimension's profile; null for a user or report dimension.</summary>
    public required DimensionProfile? Profile { get; init; }

    /// <summary>The dimension's roles.</summary>
    public required IReadOnlyList<DimensionRoleRecord> Roles { get; init; }

    /// <summary>The version whose content the snapshot holds.</summary>
    public required DimensionVersionRecord Version { get; init; }

    /// <summary>The version's levels.</summary>
    public required IReadOnlyList<DimensionLevelRecord> Levels { get; init; }

    /// <summary>The version's members.</summary>
    public required IReadOnlyList<DimensionMemberRecord> Members { get; init; }

    /// <summary>The version's range bands.</summary>
    public required IReadOnlyList<RangeBandRecord> Bands { get; init; }

    /// <summary>What surrounds the aggregate, read when it was loaded.</summary>
    public required DimensionSurroundings Surroundings { get; init; }

    /// <summary><c>dimension_version</c>.</summary>
    public string Kind => VersionKinds.DimensionVersion;

    /// <summary>The version's id.</summary>
    public long VersionId => Version.DimensionVersionId;

    /// <summary>Whether the version is the live one, which consumers read and change sets edit.</summary>
    public bool IsLive => Version.Status == PublicationStatuses.Published;

    /// <summary>The profile's edit policy; null for a dimension without a profile.</summary>
    public string? EditPolicy => Profile?.EditPolicy;
}

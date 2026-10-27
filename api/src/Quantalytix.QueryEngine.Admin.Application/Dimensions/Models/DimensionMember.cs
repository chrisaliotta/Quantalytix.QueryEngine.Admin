using System.Text.Json.Nodes;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionMember</c>.</summary>
public sealed record DimensionMember
{
    /// <summary>The member's id.</summary>
    public required string Id { get; init; }

    /// <summary>The version's id.</summary>
    public required string VersionId { get; init; }

    /// <summary>The member key, unique in the version and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The description.</summary>
    public required string? Description { get; init; }

    /// <summary>The parent member of a hierarchical dimension.</summary>
    public required string? ParentId { get; init; }

    /// <summary>The member's level.</summary>
    public required string? LevelId { get; init; }

    /// <summary>Whether source values may map to the member.</summary>
    public required bool IsAssignable { get; init; }

    /// <summary>The position among its siblings.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Free key/value for a user dimension; the profile's property schema for a system dimension.</summary>
    public required JsonObject Properties { get; init; }
}

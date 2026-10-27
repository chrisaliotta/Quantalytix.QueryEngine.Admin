using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>DimensionMemberInput</c>: a stored id updates that member, whose key stays fixed; an absent or temporary id
/// creates one, which later operations reference by the temporary id.
/// </summary>
public sealed record DimensionMemberInput
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>The member key of a new member; ignored for a stored one.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The description.</summary>
    public required string? Description { get; init; }

    /// <summary>The parent of a hierarchical dimension's member, stored or temporary; ignored for other kinds.</summary>
    public required string? ParentId { get; init; }

    /// <summary>The member's level, stored or temporary.</summary>
    public required string? LevelId { get; init; }

    /// <summary>Whether source values may map to the member.</summary>
    public required bool IsAssignable { get; init; }

    /// <summary>The position among its siblings; null appends.</summary>
    public required int? SortOrder { get; init; }

    /// <summary>The member properties.</summary>
    public required JsonObject Properties { get; init; }
}

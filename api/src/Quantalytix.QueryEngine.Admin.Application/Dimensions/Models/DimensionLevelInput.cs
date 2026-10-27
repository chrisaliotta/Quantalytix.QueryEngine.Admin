using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionLevelInput</c>: a stored id updates that level; an absent or temporary id creates one.</summary>
public sealed record DimensionLevelInput
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>The level key of a new level; ignored for a stored one.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the level holds leaf members.</summary>
    public required bool IsLeaf { get; init; }

    /// <summary>A <c>periodicity</c> value; kept for time dimensions only.</summary>
    public required string? Periodicity { get; init; }
}

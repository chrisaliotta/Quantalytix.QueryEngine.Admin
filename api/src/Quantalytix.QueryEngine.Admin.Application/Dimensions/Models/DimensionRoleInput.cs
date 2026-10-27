using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionRoleInput</c>: a stored id updates that role; an absent or temporary id creates one.</summary>
public sealed record DimensionRoleInput
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>The role key of a new role, unique in the tenant; ignored for a stored one.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The description.</summary>
    public required string? Description { get; init; }
}

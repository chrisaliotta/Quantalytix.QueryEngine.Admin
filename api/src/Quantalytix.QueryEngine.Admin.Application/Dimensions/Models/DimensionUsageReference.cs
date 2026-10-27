using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionUsageReference</c>: something outside the dimension that references it, a role or member keys.</summary>
public sealed record DimensionUsageReference
{
    /// <summary>A <c>DimensionUsageKind</c> value, such as <c>mapping_set</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The referencing object's key.</summary>
    public required string Name { get; init; }

    /// <summary>What the reference is, in a phrase.</summary>
    public required string Detail { get; init; }

    /// <summary>The role the object reads; omitted when it reads the dimension.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RoleKey { get; init; }

    /// <summary>The member keys the object names in the live version; omitted when it names none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? MemberKeys { get; init; }

    /// <summary>The version number an assumption set pins; omitted for other kinds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PinsVersion { get; init; }

    /// <summary>The object as blockers name it, such as <c>'loans.details' v3</c>; omitted when the name says it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    /// <summary>Whether a historical assumption set pins a version; the active set's pins never block a delete.</summary>
    [JsonIgnore]
    public bool IsHistoricalPin { get; init; }
}

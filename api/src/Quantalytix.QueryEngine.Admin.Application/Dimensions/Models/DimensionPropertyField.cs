using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionPropertyField</c>: one member property of a profile's schema (OI-18).</summary>
public sealed record DimensionPropertyField
{
    /// <summary>The property key in <c>properties</c>.</summary>
    public required string Key { get; init; }

    /// <summary>The label the editor shows.</summary>
    public required string Label { get; init; }

    /// <summary>A <see cref="DimensionPropertyTypes"/> value.</summary>
    public required string Type { get; init; }

    /// <summary>The choices of a <c>select</c> field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>Whether every member must have a value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Required { get; init; }

    /// <summary>Help text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary><c>geojson</c> for a text field that holds GeoJSON.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }
}

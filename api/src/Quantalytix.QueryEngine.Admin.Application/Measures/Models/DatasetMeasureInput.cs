using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>The contract's <c>DatasetMeasureInput</c>: a stored id updates that measure, whose key stays fixed; an absent or temporary id creates one.</summary>
public sealed record DatasetMeasureInput : DatasetMeasureDefinition
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
}

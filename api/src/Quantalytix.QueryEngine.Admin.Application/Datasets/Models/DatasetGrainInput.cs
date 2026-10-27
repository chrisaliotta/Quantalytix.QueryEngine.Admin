using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetGrainInput</c>: a stored id updates that grain, whose key stays fixed; an absent or temporary id creates one.</summary>
public sealed record DatasetGrainInput : DatasetGrainDefinition
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
}

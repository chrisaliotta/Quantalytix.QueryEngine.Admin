using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>The contract's <c>DatasetBindingInput</c>: a stored id updates that binding; an absent or temporary id creates one.</summary>
public sealed record DatasetBindingInput : DatasetBindingDefinition
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
}

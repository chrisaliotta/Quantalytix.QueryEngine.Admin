using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetFieldInput</c>: a stored id updates that field, whose key stays fixed; an absent or temporary id creates one.</summary>
public sealed record DatasetFieldInput : DatasetFieldDefinition
{
    /// <summary>A stored id, a temporary id, or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>For a column field on a managed table: the source column is new and is added at commit (OI-33).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CreateColumn { get; init; }

    /// <summary>For a JSON path field: materialize it once it is written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Materialize { get; init; }
}

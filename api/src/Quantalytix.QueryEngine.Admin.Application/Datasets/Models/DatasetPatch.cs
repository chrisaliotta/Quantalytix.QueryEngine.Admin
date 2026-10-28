using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetPatch</c>: the identity fields that change after creation; an absent field keeps its value.</summary>
public sealed record DatasetPatch
{
    /// <summary>The new display name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>The new availability.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; init; }
}

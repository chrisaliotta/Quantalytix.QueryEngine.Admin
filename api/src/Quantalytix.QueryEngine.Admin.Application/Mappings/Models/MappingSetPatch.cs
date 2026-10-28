using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingSetPatch</c>: the identity fields to change; key and dimension are fixed after creation.</summary>
public sealed record MappingSetPatch
{
    /// <summary>The new display name; absent keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>The new source namespace; absent keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceNamespace { get; init; }
}

using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgePatch</c>: the identity fields to change; key and endpoints are fixed after creation.</summary>
public sealed record BridgePatch
{
    /// <summary>The new display name; absent keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>The new availability; absent keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; init; }
}

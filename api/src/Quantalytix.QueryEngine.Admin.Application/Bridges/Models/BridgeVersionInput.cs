namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The editable attributes of a bridge version, as <c>PUT bridge-versions/{versionId}</c> takes them.</summary>
public record BridgeVersionInput
{
    /// <summary>A <c>cardinality</c> value: <c>many_to_one</c> or <c>many_to_many</c>.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value: <c>none</c>, <c>partition</c> or <c>replicate</c>.</summary>
    public required string AllocationMode { get; init; }
}

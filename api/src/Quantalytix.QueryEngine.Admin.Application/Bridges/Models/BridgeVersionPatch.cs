namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The contract's <c>BridgeVersionPatch</c>: the live version's settings, edited in place (OI-26); mappings that no longer comply
/// become findings.
/// </summary>
public sealed record BridgeVersionPatch
{
    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value the cardinality allows (B5).</summary>
    public required string AllocationMode { get; init; }
}

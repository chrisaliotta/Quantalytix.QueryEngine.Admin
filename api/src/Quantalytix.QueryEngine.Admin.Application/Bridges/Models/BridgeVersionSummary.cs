namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeVersionSummary</c>: one version of a bridge list item, with its settings and mapping count.</summary>
public sealed record BridgeVersionSummary
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>The version's mappings.</summary>
    public required int MappingCount { get; init; }
}

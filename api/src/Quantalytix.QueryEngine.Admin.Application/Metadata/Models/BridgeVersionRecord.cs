namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_bridge_version</c> row: one snapshot's cardinality, allocation semantics and availability of a bridge.</summary>
public sealed record BridgeVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long BridgeVersionId { get; init; }

    /// <summary>The owning bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The snapshot the version belongs to; unique together with the bridge.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

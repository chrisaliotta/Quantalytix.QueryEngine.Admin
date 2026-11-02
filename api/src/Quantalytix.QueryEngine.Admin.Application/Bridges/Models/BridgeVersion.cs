namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeVersion</c>: the live version or a frozen snapshot's version (B10).</summary>
public sealed record BridgeVersion
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The bridge's id.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>Whether the engine offers the bridge in the version's snapshot.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

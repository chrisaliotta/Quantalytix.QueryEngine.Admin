namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_snapshot</c> row: a set holding one version of every identity. Exactly one snapshot is live and edited in place; every
/// other snapshot is a frozen capture of it.
/// </summary>
public sealed record SnapshotRecord
{
    /// <summary>Primary key.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>The key requests name the snapshot by; unique and fixed.</summary>
    public required string SnapshotKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this is the live snapshot.</summary>
    public required bool IsLive { get; init; }

    /// <summary>When a frozen snapshot was captured (UTC); null for the live snapshot.</summary>
    public DateTime? CapturedAt { get; init; }

    /// <summary>The token subject of whoever captured a frozen snapshot; null for the live snapshot.</summary>
    public string? CapturedBy { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

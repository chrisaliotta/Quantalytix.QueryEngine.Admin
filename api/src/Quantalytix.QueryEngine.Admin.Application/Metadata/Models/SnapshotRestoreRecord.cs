namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_snapshot_restore</c> row: one restore of a frozen snapshot into the live snapshot.</summary>
public sealed record SnapshotRestoreRecord
{
    /// <summary>Primary key.</summary>
    public required long SnapshotRestoreId { get; init; }

    /// <summary>The frozen snapshot restored.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>When the restore ran (UTC).</summary>
    public required DateTime RestoredAt { get; init; }

    /// <summary>The token subject of the caller.</summary>
    public required string RestoredBy { get; init; }
}

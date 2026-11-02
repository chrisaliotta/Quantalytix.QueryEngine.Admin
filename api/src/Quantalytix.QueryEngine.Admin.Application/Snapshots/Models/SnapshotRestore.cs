using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>SnapshotRestore</c>: one row of the restore log, <c>qe_snapshot_restore</c>.</summary>
public sealed record SnapshotRestore
{
    /// <summary>A restore as logged.</summary>
    /// <param name="row">The restore row.</param>
    [SetsRequiredMembers]
    public SnapshotRestore(SnapshotRestoreRecord row)
    {
        SnapshotId = row.SnapshotId;
        RestoredAt = row.RestoredAt;
        RestoredBy = row.RestoredBy;
    }

    /// <summary>The frozen snapshot restored.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>When the restore ran (UTC).</summary>
    public required DateTime RestoredAt { get; init; }

    /// <summary>The token subject of the caller.</summary>
    public required string RestoredBy { get; init; }
}

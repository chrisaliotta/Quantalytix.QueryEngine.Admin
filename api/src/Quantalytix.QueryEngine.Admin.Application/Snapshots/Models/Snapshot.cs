using System.Diagnostics.CodeAnalysis;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>Snapshot</c>: one row of <c>qe_snapshot</c>.</summary>
public record Snapshot
{
    /// <summary>A snapshot as stored.</summary>
    /// <param name="row">The snapshot row.</param>
    [SetsRequiredMembers]
    public Snapshot(SnapshotRecord row)
    {
        Id = row.SnapshotId;
        Key = row.SnapshotKey;
        DisplayName = row.DisplayName;
        Description = row.Description;
        IsLive = row.IsLive;
        CapturedAt = row.CapturedAt;
        CapturedBy = row.CapturedBy;
        CreatedAt = row.CreatedAt;
    }

    /// <summary>The snapshot.</summary>
    public required long Id { get; init; }

    /// <summary>The key requests name the snapshot by, fixed after capture.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>An optional note.</summary>
    public required string? Description { get; init; }

    /// <summary>Whether this is the live snapshot, which every edit writes.</summary>
    public required bool IsLive { get; init; }

    /// <summary>When a frozen snapshot was captured (UTC); null for the live snapshot.</summary>
    public required DateTime? CapturedAt { get; init; }

    /// <summary>Who captured a frozen snapshot; null for the live snapshot.</summary>
    public required string? CapturedBy { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>The contract's <c>SnapshotListItem</c>: a snapshot with its version counts and its latest restore.</summary>
public sealed record SnapshotListItem : Snapshot
{
    /// <summary>A snapshot row with its counts and latest restore.</summary>
    /// <param name="row">The snapshot row.</param>
    /// <param name="counts">How many versions it holds of each kind.</param>
    /// <param name="lastRestore">Its latest restore; null when never restored.</param>
    [SetsRequiredMembers]
    public SnapshotListItem(SnapshotRecord row, SnapshotCounts counts, SnapshotRestore? lastRestore)
        : base(row)
    {
        Counts = counts;
        LastRestore = lastRestore;
    }

    /// <summary>How many versions the snapshot holds of each kind.</summary>
    public required SnapshotCounts Counts { get; init; }

    /// <summary>The snapshot's latest restore into live; null when never restored, and always for the live snapshot.</summary>
    public required SnapshotRestore? LastRestore { get; init; }
}

/// <summary>The contract's <c>SnapshotList</c>: the live snapshot and the frozen ones.</summary>
public sealed record SnapshotList
{
    /// <summary>The live snapshot.</summary>
    public required SnapshotListItem Live { get; init; }

    /// <summary>Every frozen snapshot, newest capture first.</summary>
    public required IReadOnlyList<SnapshotListItem> Frozen { get; init; }
}

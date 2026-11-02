namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>SnapshotDetail</c>: a snapshot with the version it holds of each identity and its restores.</summary>
public sealed record SnapshotDetail
{
    /// <summary>The snapshot.</summary>
    public required Snapshot Snapshot { get; init; }

    /// <summary>The versions it holds: kind by kind in capture order, each by key.</summary>
    public required IReadOnlyList<SnapshotEntry> Entries { get; init; }

    /// <summary>Its restores into live, newest first; none for the live snapshot.</summary>
    public required IReadOnlyList<SnapshotRestore> Restores { get; init; }
}

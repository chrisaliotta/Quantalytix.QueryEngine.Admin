namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>One version a snapshot holds: the snapshot and the entry.</summary>
/// <param name="SnapshotId">The snapshot.</param>
/// <param name="Entry">The identity and version.</param>
public sealed record SnapshotHolding(long SnapshotId, SnapshotEntry Entry);

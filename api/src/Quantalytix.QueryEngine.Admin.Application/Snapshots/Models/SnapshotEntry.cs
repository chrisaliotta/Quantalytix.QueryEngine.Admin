namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>The contract's <c>SnapshotEntry</c>: the version a snapshot holds of one identity.</summary>
public sealed record SnapshotEntry
{
    /// <summary>A <c>SnapshotKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The identity's key.</summary>
    public required string Key { get; init; }

    /// <summary>The identity's display name, as the version names it for models and reports.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The version the snapshot holds, which the kind's version read shows.</summary>
    public required long VersionId { get; init; }
}

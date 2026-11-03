namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>
/// The contract's <c>SnapshotComparison</c>: the identities <c>to</c> holds that <c>from</c> does not, the reverse, and those both hold,
/// each with the version on its side.
/// </summary>
public sealed record SnapshotComparison
{
    /// <summary>The snapshot compared from.</summary>
    public required long FromSnapshotId { get; init; }

    /// <summary>The snapshot compared to.</summary>
    public required long ToSnapshotId { get; init; }

    /// <summary>Identities only <c>to</c> holds, with <c>to</c>'s version.</summary>
    public required IReadOnlyList<SnapshotEntry> Added { get; init; }

    /// <summary>Identities only <c>from</c> holds, with <c>from</c>'s version.</summary>
    public required IReadOnlyList<SnapshotEntry> Removed { get; init; }

    /// <summary>Identities both hold, each with both versions and whether their content differs.</summary>
    public required IReadOnlyList<SnapshotPair> Common { get; init; }
}

/// <summary>The contract's <c>SnapshotPair</c>: an identity both compared snapshots hold, with each one's version.</summary>
public sealed record SnapshotPair
{
    /// <summary>A <c>SnapshotKind</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The identity's key.</summary>
    public required string Key { get; init; }

    /// <summary>The version <c>from</c> holds.</summary>
    public required long FromVersionId { get; init; }

    /// <summary>The version <c>to</c> holds.</summary>
    public required long ToVersionId { get; init; }

    /// <summary>Whether the two versions' content differs as the engine reads it, compared by keys rather than surrogate ids.</summary>
    public required bool Differs { get; init; }
}

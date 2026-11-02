namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersionSummary</c>: one version of a list item, with its snapshot and member count.</summary>
public sealed record DimensionVersionSummary
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required string SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>The version's members.</summary>
    public required int MemberCount { get; init; }
}

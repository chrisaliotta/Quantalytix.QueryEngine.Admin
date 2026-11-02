namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingVersionSummary</c>: one version of a mapping set list item, with its entry count.</summary>
public sealed record MappingVersionSummary
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>The version's entries.</summary>
    public required int EntryCount { get; init; }
}

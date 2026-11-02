namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingVersion</c>: the live version or a frozen snapshot's version (B10).</summary>
public sealed record MappingVersion
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The mapping set's id.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

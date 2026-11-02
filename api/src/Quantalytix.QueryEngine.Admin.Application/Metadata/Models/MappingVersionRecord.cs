namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_mapping_version</c> row: one snapshot's source-key mapping content of a mapping set.</summary>
public sealed record MappingVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long MappingVersionId { get; init; }

    /// <summary>The owning mapping set.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>The snapshot the version belongs to; unique together with the mapping set.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

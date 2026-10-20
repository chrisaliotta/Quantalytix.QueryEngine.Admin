namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_mapping_version</c> row: source-key mapping content of a mapping set, of which at most one version is published.</summary>
public sealed record MappingVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long MappingVersionId { get; init; }

    /// <summary>The owning mapping set.</summary>
    public required long MappingSetId { get; init; }

    /// <summary>Monotonic version within the mapping set.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

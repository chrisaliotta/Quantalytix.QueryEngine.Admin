namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_bridge_version</c> row: pins the source and target dimension versions and the allocation semantics of a bridge.</summary>
public sealed record BridgeVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long BridgeVersionId { get; init; }

    /// <summary>The owning bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>Version number within the bridge.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The pinned source dimension version.</summary>
    public required long SourceDimensionVersionId { get; init; }

    /// <summary>The pinned target dimension version.</summary>
    public required long TargetDimensionVersionId { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

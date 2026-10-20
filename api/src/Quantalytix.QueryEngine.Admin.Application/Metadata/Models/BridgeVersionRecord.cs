namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_bridge_version</c> row: the cardinality, allocation semantics and assignments of a bridge, of which at most one version is published.</summary>
public sealed record BridgeVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long BridgeVersionId { get; init; }

    /// <summary>The owning bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>Version number within the bridge.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

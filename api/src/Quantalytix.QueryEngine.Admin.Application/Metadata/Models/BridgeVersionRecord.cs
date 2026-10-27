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

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the version was retired (UTC); null until then.</summary>
    public DateTime? RetiredAt { get; init; }

    /// <summary>Why the version was frozen, such as <c>FY26 close</c>; null while live or when frozen without a note.</summary>
    public string? FreezeNote { get; init; }
}

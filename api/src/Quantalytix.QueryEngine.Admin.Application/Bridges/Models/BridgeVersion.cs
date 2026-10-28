namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeVersion</c>: live (stored <c>published</c>) or frozen (stored <c>retired</c>) (B10).</summary>
public sealed record BridgeVersion
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The bridge's id.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The version number within the bridge.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>cardinality</c> value.</summary>
    public required string Cardinality { get; init; }

    /// <summary>An <c>allocation_mode</c> value.</summary>
    public required string AllocationMode { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the version went live (UTC).</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>When the version was frozen (UTC).</summary>
    public required DateTime? RetiredAt { get; init; }

    /// <summary>Why the version was frozen.</summary>
    public required string? FreezeNote { get; init; }
}

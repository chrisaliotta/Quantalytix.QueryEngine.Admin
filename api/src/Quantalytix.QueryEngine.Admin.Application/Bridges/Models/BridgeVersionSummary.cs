namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeVersionSummary</c>: one version of a bridge list item, with its settings and mapping count.</summary>
public sealed record BridgeVersionSummary
{
    /// <summary>The version's id.</summary>
    public required long Id { get; init; }

    /// <summary>The version number within the bridge.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value: <c>published</c> is live, <c>retired</c> frozen.</summary>
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

    /// <summary>The version's mappings.</summary>
    public required int MappingCount { get; init; }
}

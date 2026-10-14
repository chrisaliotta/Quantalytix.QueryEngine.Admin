namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set</c> row: an optional, manually created snapshot that pins exact dimension versions for reproducible reporting.</summary>
public sealed record AssumptionSetRecord
{
    /// <summary>Primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>Stable logical name such as <c>FY2026_Q3_CLOSE</c>.</summary>
    public required string AssumptionSetKey { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional business purpose or provenance.</summary>
    public string? Description { get; init; }

    /// <summary>Semantic timestamp (UTC) used to resolve which versions to capture.</summary>
    public required DateTime EffectiveAt { get; init; }

    /// <summary>An <c>assumption_set_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>Optional deterministic hash of the pinned-version manifest.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Finalization time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_assumption_set</c> row: a named set of version pins. The one active set pins every published version and every
/// publish repoints it; a historical set is a snapshot of the active set and the active release captured under its own key.
/// </summary>
public sealed record AssumptionSetRecord
{
    /// <summary>Primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>Stable key a query or report request names, such as <c>FY2026_Q3_CLOSE</c>; the active set's is <c>active</c>.</summary>
    public required string AssumptionSetKey { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional note, such as what a snapshot captured.</summary>
    public string? Description { get; init; }

    /// <summary>An <c>assumption_set_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// The <c>qe_semantic_release.release_id</c> a historical set reads dataset versions from: the release that was active when it
    /// was captured. Null for the active set, which reads the active release.
    /// </summary>
    public long? ReleaseId { get; init; }

    /// <summary>When a historical set was captured (UTC); null for the active set.</summary>
    public DateTime? CapturedAt { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

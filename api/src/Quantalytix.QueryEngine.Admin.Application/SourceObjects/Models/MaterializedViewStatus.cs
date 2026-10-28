namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A materialized view's refresh status as StarRocks reports it.</summary>
public sealed record MaterializedViewStatus
{
    /// <summary>A <c>MaterializationStates</c> value.</summary>
    public required string State { get; init; }

    /// <summary>When the view was created, which each change of its columns does (UTC).</summary>
    public required DateTime DefinedAt { get; init; }

    /// <summary>When the last refresh finished (UTC); null before the first.</summary>
    public required DateTime? RefreshedAt { get; init; }

    /// <summary>Why the view is inactive or its last refresh failed; null otherwise.</summary>
    public required string? Error { get; init; }
}

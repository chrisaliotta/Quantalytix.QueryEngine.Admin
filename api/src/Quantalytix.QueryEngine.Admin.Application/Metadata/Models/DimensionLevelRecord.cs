namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_level</c> row: an optional business label for a hierarchy level of a dimension version.</summary>
public sealed record DimensionLevelRecord
{
    /// <summary>Primary key.</summary>
    public required long LevelId { get; init; }

    /// <summary>The owning dimension version.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Stable level name.</summary>
    public required string LevelKey { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Business ordering of levels; not the authoritative tree depth.</summary>
    public required int Ordinal { get; init; }

    /// <summary>A <c>periodicity</c> value for time-oriented levels; null otherwise.</summary>
    public string? Periodicity { get; init; }

    /// <summary>Whether this is the business-declared leaf level.</summary>
    public required bool IsLeafLevel { get; init; }
}

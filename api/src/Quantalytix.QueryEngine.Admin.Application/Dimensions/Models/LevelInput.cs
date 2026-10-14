namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable attributes of a level, as <c>PUT dimension-versions/{dimensionVersionId}/levels/{levelId}</c> takes them.</summary>
public record LevelInput
{
    /// <summary>Human-readable label, at most 256 characters.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Business ordering of the levels; not the authoritative tree depth.</summary>
    public required int Ordinal { get; init; }

    /// <summary>A <c>periodicity</c> value for a time-oriented level; null otherwise.</summary>
    public string? Periodicity { get; init; }

    /// <summary>Whether this is the business-declared leaf level.</summary>
    public required bool IsLeafLevel { get; init; }
}

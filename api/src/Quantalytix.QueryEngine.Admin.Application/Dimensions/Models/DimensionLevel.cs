namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionLevel</c>.</summary>
public sealed record DimensionLevel
{
    /// <summary>The level's id.</summary>
    public required string Id { get; init; }

    /// <summary>The version's id.</summary>
    public required string VersionId { get; init; }

    /// <summary>The level key, unique in the version and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The position from the top, starting at 1.</summary>
    public required int Ordinal { get; init; }

    /// <summary>Whether the level holds leaf members.</summary>
    public required bool IsLeaf { get; init; }

    /// <summary>A <c>periodicity</c> value for a time dimension; null otherwise.</summary>
    public required string? Periodicity { get; init; }
}

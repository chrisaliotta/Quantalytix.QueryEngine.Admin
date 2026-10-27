namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersionContent</c>: a version with its levels, members and bands.</summary>
public sealed record DimensionVersionContent
{
    /// <summary>The version.</summary>
    public required DimensionVersion Version { get; init; }

    /// <summary>The levels, by ordinal.</summary>
    public required IReadOnlyList<DimensionLevel> Levels { get; init; }

    /// <summary>The members, by sort order.</summary>
    public required IReadOnlyList<DimensionMember> Members { get; init; }

    /// <summary>The range bands, by ordinal.</summary>
    public required IReadOnlyList<DimensionRangeBand> Bands { get; init; }
}

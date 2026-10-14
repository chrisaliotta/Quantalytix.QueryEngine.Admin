namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A band's bounds in its version's bound family, comparable whatever the family's type; a null bound is unbounded.</summary>
public sealed record BandBounds
{
    /// <summary>The lower bound, or null for no lower limit.</summary>
    public IComparable? Lower { get; init; }

    /// <summary>The upper bound, or null for no upper limit.</summary>
    public IComparable? Upper { get; init; }

    /// <summary>Whether a value equal to the lower bound falls in the band.</summary>
    public required bool LowerInclusive { get; init; }

    /// <summary>Whether a value equal to the upper bound falls in the band.</summary>
    public required bool UpperInclusive { get; init; }
}

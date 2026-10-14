namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A range band route: a version of a <c>range</c> dimension, the only kind that has bands.</summary>
public sealed record RangeVersionReference
{
    /// <summary>The dimension version whose bands are read or replaced.</summary>
    public required long DimensionVersionId { get; init; }
}

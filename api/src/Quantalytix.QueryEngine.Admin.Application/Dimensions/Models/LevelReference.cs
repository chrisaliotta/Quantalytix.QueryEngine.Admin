namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A level route: the dimension version and one of its levels.</summary>
public sealed record LevelReference
{
    /// <summary>The dimension version that owns the level.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>The level.</summary>
    public required long DimensionLevelId { get; init; }
}

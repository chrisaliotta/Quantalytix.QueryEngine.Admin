namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A new level of a draft dimension version, as <c>POST dimension-versions/{dimensionVersionId}/levels</c> takes it.</summary>
public sealed record NewLevelInput : LevelInput
{
    /// <summary>Stable level key, fixed once created; uniqueness within the version is a publication rule.</summary>
    public required string LevelKey { get; init; }
}

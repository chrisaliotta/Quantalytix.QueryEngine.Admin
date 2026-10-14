namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>Identifies a measure of a dataset version from the route; the measure must belong to the version.</summary>
public sealed record MeasureReference
{
    /// <summary>The dataset version that owns the measure.</summary>
    public required long VersionId { get; init; }

    /// <summary>The measure.</summary>
    public required long MeasureId { get; init; }
}

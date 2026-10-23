namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Identifies a grain of a dataset version from the route; the grain must belong to the version.</summary>
public sealed record GrainReference
{
    /// <summary>The dataset version that owns the grain.</summary>
    public required long VersionId { get; init; }

    /// <summary>The grain.</summary>
    public required long DatasetGrainId { get; init; }
}

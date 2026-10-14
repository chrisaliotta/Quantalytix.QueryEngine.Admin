namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>One manifest row: the dataset version a release pins for a dataset.</summary>
public sealed record ReleaseDatasetInput
{
    /// <summary>The logical dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The version of the dataset that the release pins.</summary>
    public required long DatasetVersionId { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_release_dataset</c> row: pins exactly one structural dataset version per logical dataset in a semantic release.</summary>
public sealed record ReleaseDatasetRecord
{
    /// <summary>The semantic release; first part of the primary key.</summary>
    public required long ReleaseId { get; init; }

    /// <summary>The logical dataset; second part of the primary key.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The dataset version the release pins.</summary>
    public required long DatasetVersionId { get; init; }
}

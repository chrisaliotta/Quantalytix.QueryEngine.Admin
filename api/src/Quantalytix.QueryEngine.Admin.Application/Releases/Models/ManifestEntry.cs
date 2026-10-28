namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>ManifestEntry</c>: one pinned dataset version, a row of <c>qe_release_dataset</c>.</summary>
public sealed record ManifestEntry
{
    /// <summary>The dataset.</summary>
    public required long DatasetId { get; init; }

    /// <summary>The dataset version the release pins.</summary>
    public required long DatasetVersionId { get; init; }
}

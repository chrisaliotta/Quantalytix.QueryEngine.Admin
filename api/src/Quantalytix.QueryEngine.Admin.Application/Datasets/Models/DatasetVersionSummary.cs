namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetVersionSummary</c>: one version's lifecycle state and the releases that pin it.</summary>
public record DatasetVersionSummary
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dataset's id.</summary>
    public required string DatasetId { get; init; }

    /// <summary>The version number within the dataset.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>The table or view the version reads.</summary>
    public required string SourceObject { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Publication time (UTC); null until published.</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>The version this one was cloned from; null for a version created empty (OI-12).</summary>
    public required string? ClonedFromVersionId { get; init; }

    /// <summary>The releases that pin the version.</summary>
    public required IReadOnlyList<DatasetReleasePin> Releases { get; init; }
}

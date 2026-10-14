namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dataset</c> row: the stable logical dataset identity that a request's <c>datasetId</c> resolves.</summary>
public sealed record DatasetRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long DatasetId { get; init; }

    /// <summary>Stable logical dataset ID exposed to requests.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}

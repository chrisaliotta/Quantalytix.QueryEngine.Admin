namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_version</c> row: a version of one canonical dimension, of which at most one is published.</summary>
public sealed record DimensionVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>The owning dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>Monotonic version within the dimension.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>range_data_type</c> value for range dimensions; null otherwise.</summary>
    public string? RangeDataType { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

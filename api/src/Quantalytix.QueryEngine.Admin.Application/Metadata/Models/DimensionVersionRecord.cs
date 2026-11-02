namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_dimension_version</c> row: one snapshot's version of a canonical dimension.</summary>
public sealed record DimensionVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>The owning dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>The snapshot the version belongs to; unique together with the dimension.</summary>
    public required long SnapshotId { get; init; }

    /// <summary>A <c>range_data_type</c> value for range dimensions; null otherwise.</summary>
    public string? RangeDataType { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

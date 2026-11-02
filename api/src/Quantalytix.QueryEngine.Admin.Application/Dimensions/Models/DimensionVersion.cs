namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersion</c>: the live version or a frozen snapshot's version.</summary>
public sealed record DimensionVersion
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dimension's id.</summary>
    public required string DimensionId { get; init; }

    /// <summary>The snapshot the version belongs to.</summary>
    public required string SnapshotId { get; init; }

    /// <summary>Whether the version is live, and so editable; a frozen version is read-only.</summary>
    public required bool IsLive { get; init; }

    /// <summary>A <c>range_data_type</c> value for a range dimension; null otherwise.</summary>
    public required string? RangeDataType { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

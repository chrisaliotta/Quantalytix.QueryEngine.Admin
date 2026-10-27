namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionVersion</c>: live (stored <c>published</c>) or frozen (stored <c>retired</c>).</summary>
public sealed record DimensionVersion
{
    /// <summary>The version's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dimension's id.</summary>
    public required string DimensionId { get; init; }

    /// <summary>The version number within the dimension.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>range_data_type</c> value for a range dimension; null otherwise.</summary>
    public required string? RangeDataType { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the version went live (UTC).</summary>
    public required DateTime? PublishedAt { get; init; }

    /// <summary>When the version was frozen (UTC).</summary>
    public required DateTime? RetiredAt { get; init; }

    /// <summary>The next version number once frozen.</summary>
    public required int? SupersededBy { get; init; }

    /// <summary>Why the version was frozen.</summary>
    public required string? FreezeNote { get; init; }
}

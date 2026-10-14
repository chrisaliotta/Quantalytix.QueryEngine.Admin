namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_range_band</c> row: a typed range that assigns scalar source values to a member of a range dimension.</summary>
public sealed record RangeBandRecord
{
    /// <summary>The dimension version; first part of the primary key.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Evaluation order; second part of the primary key.</summary>
    public required int Ordinal { get; init; }

    /// <summary>The member assigned to the band.</summary>
    public required long MemberId { get; init; }

    /// <summary>Decimal lower boundary; null means unbounded.</summary>
    public decimal? LowerDecimal { get; init; }

    /// <summary>Decimal upper boundary; null means unbounded.</summary>
    public decimal? UpperDecimal { get; init; }

    /// <summary>Date lower boundary; null means unbounded.</summary>
    public DateTime? LowerDate { get; init; }

    /// <summary>Date upper boundary; null means unbounded.</summary>
    public DateTime? UpperDate { get; init; }

    /// <summary>Datetime lower boundary (UTC); null means unbounded.</summary>
    public DateTime? LowerDatetime { get; init; }

    /// <summary>Datetime upper boundary (UTC); null means unbounded.</summary>
    public DateTime? UpperDatetime { get; init; }

    /// <summary>Whether the lower boundary is included.</summary>
    public required bool LowerInclusive { get; init; }

    /// <summary>Whether the upper boundary is included.</summary>
    public required bool UpperInclusive { get; init; }
}

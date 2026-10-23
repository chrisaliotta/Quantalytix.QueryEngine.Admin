namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// One band of a range dimension version. Only the bound family of the version's range data type may be set, which
/// publication checks; a null bound is unbounded.
/// </summary>
public sealed record RangeBandInput
{
    /// <summary>The member the band assigns source values to.</summary>
    public required long DimensionMemberId { get; init; }

    /// <summary>Decimal lower bound, for <c>range_data_type</c> decimal.</summary>
    public decimal? LowerDecimal { get; init; }

    /// <summary>Decimal upper bound, for <c>range_data_type</c> decimal.</summary>
    public decimal? UpperDecimal { get; init; }

    /// <summary>Date lower bound (<c>yyyy-MM-dd</c>), for <c>range_data_type</c> date.</summary>
    public DateTime? LowerDate { get; init; }

    /// <summary>Date upper bound (<c>yyyy-MM-dd</c>), for <c>range_data_type</c> date.</summary>
    public DateTime? UpperDate { get; init; }

    /// <summary>Datetime lower bound (UTC), for <c>range_data_type</c> datetime.</summary>
    public DateTime? LowerDatetime { get; init; }

    /// <summary>Datetime upper bound (UTC), for <c>range_data_type</c> datetime.</summary>
    public DateTime? UpperDatetime { get; init; }

    /// <summary>Whether a value equal to the lower bound falls in the band.</summary>
    public required bool LowerInclusive { get; init; }

    /// <summary>Whether a value equal to the upper bound falls in the band.</summary>
    public required bool UpperInclusive { get; init; }
}

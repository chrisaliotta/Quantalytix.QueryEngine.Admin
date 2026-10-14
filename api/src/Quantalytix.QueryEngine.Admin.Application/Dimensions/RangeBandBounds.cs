using System.Diagnostics;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads range band bounds the way the query engine evaluates them: a value is in a band when it is above the lower bound
/// (or equal to it, when inclusive) and below the upper bound (or equal, when inclusive), and a null bound is unbounded.
/// </summary>
public static class RangeBandBounds
{
    /// <summary>The band's bounds in the family of a range data type.</summary>
    /// <param name="band">A range band.</param>
    /// <param name="rangeDataType">The version's <c>range_data_type</c> value.</param>
    /// <returns>The family's lower and upper bounds with the band's inclusivity.</returns>
    public static BandBounds Of(RangeBandRecord band, string rangeDataType)
    {
        return rangeDataType switch
        {
            RangeDataTypes.Decimal => Bounds(band.LowerDecimal, band.UpperDecimal, band),
            RangeDataTypes.Date => Bounds(band.LowerDate, band.UpperDate, band),
            RangeDataTypes.Datetime => Bounds(band.LowerDatetime, band.UpperDatetime, band),
            _ => throw new UnreachableException($"Range data type '{rangeDataType}' has no bound family.")
        };
    }

    /// <summary>The bound families in which a band sets at least one bound.</summary>
    /// <param name="band">A range band.</param>
    /// <returns><c>range_data_type</c> values, in vocabulary order.</returns>
    public static IReadOnlyList<string> FamiliesOf(RangeBandRecord band)
    {
        var families = new List<string>();
        if (band.LowerDecimal is not null || band.UpperDecimal is not null) families.Add(RangeDataTypes.Decimal);
        if (band.LowerDate is not null || band.UpperDate is not null) families.Add(RangeDataTypes.Date);
        if (band.LowerDatetime is not null || band.UpperDatetime is not null) families.Add(RangeDataTypes.Datetime);
        return families;
    }

    /// <summary>
    /// Whether a band lies entirely after the previous one: the previous band has an upper bound, this band has a lower
    /// bound, and the lower is above the upper, or equal to it with at most one of the two sides inclusive.
    /// </summary>
    /// <param name="band">The later band in ordinal order.</param>
    /// <param name="previous">The band just before it.</param>
    /// <returns>True when no value falls in both bands.</returns>
    public static bool StartsAfter(BandBounds band, BandBounds previous)
    {
        if (previous.Upper is null || band.Lower is null) return false;
        var order = previous.Upper.CompareTo(band.Lower);
        return order < 0 || (order == 0 && !(previous.UpperInclusive && band.LowerInclusive));
    }

    /// <summary>Bounds of one family's type.</summary>
    private static BandBounds Bounds<T>(T? lower, T? upper, RangeBandRecord band) where T : struct, IComparable
    {
        return new BandBounds { Lower = lower, Upper = upper, LowerInclusive = band.LowerInclusive, UpperInclusive = band.UpperInclusive };
    }
}

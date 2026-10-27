using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>band_bounds_ordered</c>: when a band has both bounds, the lower one is below the upper one, or it matches nothing.</summary>
public sealed class BandBoundsOrderedRule : DimensionBandRule
{
    /// <summary><c>band_bounds_ordered</c>.</summary>
    protected override string Code => "band_bounds_ordered";

    /// <summary>Reports every band whose lower bound is not below its upper bound, in the version's family.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>One violation per such band.</returns>
    protected override IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands)
    {
        if (snapshot.Version.RangeDataType is not { } family) return [];
        return bands
            .Where(band => RangeBandBounds.Of(band, family) is { Lower: { } lower, Upper: { } upper } && lower.CompareTo(upper) >= 0)
            .Select(band => BandViolation(band, $"{Describe(snapshot, band)} has a lower bound that is not below its upper bound."));
    }
}

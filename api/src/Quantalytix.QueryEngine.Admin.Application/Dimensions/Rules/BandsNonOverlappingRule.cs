using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>bands_non_overlapping</c>: in ordinal order, each band starts after the previous one ends, taking inclusivity into account,
/// so no source value falls in two bands and duplicates its fact row.
/// </summary>
public sealed class BandsNonOverlappingRule : DimensionBandRule
{
    /// <summary><c>bands_non_overlapping</c>.</summary>
    protected override string Code => "bands_non_overlapping";

    /// <summary>Compares each band with the one before it, in the version's family.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>One violation per band that does not start after its predecessor ends.</returns>
    protected override IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands)
    {
        if (snapshot.Version.RangeDataType is not { } family) yield break;
        for (var index = 1; index < bands.Count; index++)
        {
            var previous = bands[index - 1];
            var band = bands[index];
            if (RangeBandBounds.StartsAfter(RangeBandBounds.Of(band, family), RangeBandBounds.Of(previous, family))) continue;
            yield return BandViolation(band, $"{Describe(snapshot, band)} overlaps {Describe(snapshot, previous)}: each band must start after the previous one ends.");
        }
    }
}

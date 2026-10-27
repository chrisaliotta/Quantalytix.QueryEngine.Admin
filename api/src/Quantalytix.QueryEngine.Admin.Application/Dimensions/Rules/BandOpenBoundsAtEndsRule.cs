using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>band_open_bounds_at_ends</c>: only the first band may be open below and only the last open above, since an open bound
/// anywhere else reaches into its neighbour.
/// </summary>
public sealed class BandOpenBoundsAtEndsRule : DimensionBandRule
{
    /// <summary><c>band_open_bounds_at_ends</c>.</summary>
    protected override string Code => "band_open_bounds_at_ends";

    /// <summary>Reports every band after the first without a lower bound and every band before the last without an upper one.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>One violation per open bound in the middle.</returns>
    protected override IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands)
    {
        if (snapshot.Version.RangeDataType is not { } family) yield break;
        for (var index = 0; index < bands.Count; index++)
        {
            var bounds = RangeBandBounds.Of(bands[index], family);
            if (index > 0 && bounds.Lower is null) yield return BandViolation(bands[index], $"{Describe(snapshot, bands[index])} is open below, but only the first band may be.");
            if (index < bands.Count - 1 && bounds.Upper is null) yield return BandViolation(bands[index], $"{Describe(snapshot, bands[index])} is open above, but only the last band may be.");
        }
    }
}

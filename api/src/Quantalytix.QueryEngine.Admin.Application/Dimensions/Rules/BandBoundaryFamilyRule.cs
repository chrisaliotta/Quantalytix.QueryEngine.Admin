using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>band_boundary_family</c>: a range version's bands set only the bounds of its range data type's family, because the engine
/// compares source values against that family's columns alone.
/// </summary>
public sealed class BandBoundaryFamilyRule : DimensionBandRule
{
    /// <summary><c>band_boundary_family</c>.</summary>
    protected override string Code => "band_boundary_family";

    /// <summary>Reports every band that sets a bound of another family; without a range data type the family is unknown.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>One violation per band with foreign bounds.</returns>
    protected override IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands)
    {
        if (snapshot.Version.RangeDataType is not { } family) return [];
        return bands
            .Select(band => (Band: band, Foreign: RangeBandBounds.FamiliesOf(band).Where(used => used != family).ToList()))
            .Where(check => check.Foreign.Count > 0)
            .Select(check => BandViolation(
                check.Band,
                $"{Describe(snapshot, check.Band)} sets {string.Join(" and ", check.Foreign)} bounds, but the version's range data type is {family}."));
    }
}

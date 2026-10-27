using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>band_member_in_version</c>: every band assigns a member of its own version, since member ids are global.</summary>
public sealed class BandMemberInVersionRule : DimensionBandRule
{
    /// <summary><c>band_member_in_version</c>.</summary>
    protected override string Code => "band_member_in_version";

    /// <summary>Reports every band whose member is not a member of the version.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>One violation per such band.</returns>
    protected override IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands)
    {
        var ids = snapshot.Members.Select(member => member.DimensionMemberId).ToHashSet();
        return bands
            .Where(band => !ids.Contains(band.DimensionMemberId))
            .Select(band => BandViolation(band, $"{Describe(snapshot, band)} assigns a member that is not a member of this version."));
    }
}

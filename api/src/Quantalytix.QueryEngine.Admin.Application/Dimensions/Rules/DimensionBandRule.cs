using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// A range band rule: a publication finding, since bands are edited a set at a time and checked as a whole before freezing. Only
/// range dimensions use bands.
/// </summary>
public abstract class DimensionBandRule : DimensionRule
{
    /// <summary>Publication.</summary>
    public override RuleStage Stage => RuleStage.Publication;

    /// <summary>Checks the bands of a range dimension's version, by ordinal.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>The violations; none for another kind.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        if (snapshot.Dimension.DimensionKind != DimensionKinds.Range) return [];
        return EvaluateBands(snapshot, [.. snapshot.Bands.OrderBy(band => band.Ordinal)]);
    }

    /// <summary>The violations of a version's bands.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="bands">Its bands by ordinal.</param>
    /// <returns>The violations.</returns>
    protected abstract IEnumerable<RuleViolation> EvaluateBands(DimensionSnapshot snapshot, IReadOnlyList<RangeBandRecord> bands);

    /// <summary>A band as messages name it, such as <c>Band 2 (member 'LOW')</c>; a member outside the version shows its id.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <param name="band">One of its bands.</param>
    /// <returns>The band's ordinal and member.</returns>
    protected static string Describe(DimensionSnapshot snapshot, RangeBandRecord band)
    {
        var member = snapshot.Members.SingleOrDefault(row => row.DimensionMemberId == band.DimensionMemberId);
        return member is null ? $"Band {band.Ordinal} (member {band.DimensionMemberId})" : $"Band {band.Ordinal} (member '{member.MemberKey}')";
    }

    /// <summary>A violation on a band, targeted by its ordinal.</summary>
    /// <param name="band">The band.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation BandViolation(RangeBandRecord band, string message)
    {
        return Violation(DimensionTabs.Bands, message, band.Ordinal);
    }
}

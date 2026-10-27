using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>profile_properties_valid</c> (OI-18): every member of a system dimension has the properties its profile's schema requires,
/// with the field types. A publication finding, because members stored before the schema changed keep loading.
/// </summary>
public sealed class ProfilePropertiesValidRule : DimensionRule
{
    /// <summary>Publication.</summary>
    public override RuleStage Stage => RuleStage.Publication;

    /// <summary><c>profile_properties_valid</c>.</summary>
    protected override string Code => "profile_properties_valid";

    /// <summary>Reports every property problem of every member.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per problem.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        if (snapshot.Profile is not { } profile) return [];
        return snapshot.Members.SelectMany(member => DimensionPropertyRules
            .Problems(profile, DimensionReadModels.PropertiesOf(member.Properties))
            .Select(problem => Violation(DimensionTabs.Members, $"Member '{member.MemberKey}': {problem.Message}", member.DimensionMemberId)));
    }
}

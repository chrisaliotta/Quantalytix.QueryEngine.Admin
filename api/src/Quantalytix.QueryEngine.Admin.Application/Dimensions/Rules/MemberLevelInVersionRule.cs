using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>member_level_in_version</c>: a member's level is a level of the same version.</summary>
public sealed class MemberLevelInVersionRule : DimensionRule
{
    /// <summary>Gate: level groupings resolve through these levels.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>member_level_in_version</c>.</summary>
    protected override string Code => "member_level_in_version";

    /// <summary>Reports every member whose level is not in the version.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per such member.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        var ids = snapshot.Levels.Select(level => level.DimensionLevelId).ToHashSet();
        return snapshot.Members
            .Where(member => member.DimensionLevelId is { } levelId && !ids.Contains(levelId))
            .Select(member => Violation(DimensionTabs.Members, $"Member '{member.MemberKey}' has a level that is not a level of this version.", member.DimensionMemberId));
    }
}

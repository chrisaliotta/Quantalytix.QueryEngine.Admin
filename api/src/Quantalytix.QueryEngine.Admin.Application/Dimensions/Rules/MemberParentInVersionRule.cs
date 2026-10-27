using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>member_parent_in_version</c>: a member's parent is a member of the same version, since member ids are global.</summary>
public sealed class MemberParentInVersionRule : DimensionRule
{
    /// <summary>Gate: the closure is built from these parents.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>member_parent_in_version</c>.</summary>
    protected override string Code => "member_parent_in_version";

    /// <summary>Reports every member whose parent is not in the version.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per such member.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        var ids = snapshot.Members.Select(member => member.DimensionMemberId).ToHashSet();
        return snapshot.Members
            .Where(member => member.ParentDimensionMemberId is { } parentId && !ids.Contains(parentId))
            .Select(member => Violation(DimensionTabs.Members, $"Member '{member.MemberKey}' has a parent that is not a member of this version.", member.DimensionMemberId));
    }
}

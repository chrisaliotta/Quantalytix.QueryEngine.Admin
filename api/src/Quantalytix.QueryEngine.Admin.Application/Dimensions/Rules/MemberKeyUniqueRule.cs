using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>member_key_unique</c>: no two members of a version share a key, because every consumer names members by key.</summary>
public sealed class MemberKeyUniqueRule : DimensionRule
{
    /// <summary>Gate: the live version is read as it is saved.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>member_key_unique</c>.</summary>
    protected override string Code => "member_key_unique";

    /// <summary>Reports every key more than one member uses, at its second member.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per duplicated key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        return snapshot.Members
            .GroupBy(member => member.MemberKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => Violation(
                DimensionTabs.Members,
                $"Member key '{group.Key}' is used by {group.Count()} members: {string.Join(", ", group.Select(member => member.DisplayName))}.",
                group.ElementAt(1).DimensionMemberId));
    }
}

using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>hierarchy_acyclic</c>: following parent links never returns to a member already passed. A cycle has no root, so its
/// members would get no closure rows and would group nowhere.
/// </summary>
public sealed class HierarchyAcyclicRule : DimensionRule
{
    /// <summary>Gate: the closure is built from these parents.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>hierarchy_acyclic</c>.</summary>
    protected override string Code => "hierarchy_acyclic";

    /// <summary>
    /// Walks up from every member, stopping at a root, at a parent outside the version, or at a member an earlier walk finished;
    /// returning to the current walk's own path is a cycle, reported once with the members on it.
    /// </summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per cycle.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        var byId = snapshot.Members.ToDictionary(member => member.DimensionMemberId);
        var finished = new HashSet<long>();
        foreach (var start in snapshot.Members.OrderBy(member => member.MemberKey, StringComparer.Ordinal))
        {
            var path = new List<long>();
            var onPath = new HashSet<long>();
            long? current = start.DimensionMemberId;
            while (current is { } memberId && byId.ContainsKey(memberId) && !finished.Contains(memberId) && onPath.Add(memberId))
            {
                path.Add(memberId);
                current = byId[memberId].ParentDimensionMemberId;
            }

            if (current is { } repeated && onPath.Contains(repeated)) yield return Cycle(path[path.IndexOf(repeated)..], byId);
            finished.UnionWith(path);
        }
    }

    /// <summary>A violation naming the members of a cycle in parent order, ending where it started.</summary>
    private RuleViolation Cycle(List<long> cycle, Dictionary<long, DimensionMemberRecord> byId)
    {
        var keys = cycle.Append(cycle[0]).Select(memberId => $"'{byId[memberId].MemberKey}'");
        return Violation(DimensionTabs.Members, $"Members {string.Join(" -> ", keys)} form a parent cycle.", cycle[0]);
    }
}

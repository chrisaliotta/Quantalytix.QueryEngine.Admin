using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>hierarchy_acyclic</c>: following parent links never returns to a member already passed. A cycle has no root, so its
/// members would get no closure rows and would group nowhere.
/// </summary>
public sealed class HierarchyAcyclicRule(ITableGateway<DimensionMemberRecord> members) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "hierarchy_acyclic";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>
    /// Walks up from every member, stopping at a root, at a parent outside the version, or at a member an earlier walk
    /// finished; returning to the current walk's own path is a cycle, reported once with the members on it.
    /// </summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per cycle.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var byId = rows.ToDictionary(member => member.MemberId);
        var finished = new HashSet<long>();
        var violations = new List<RuleViolation>();
        foreach (var start in rows.OrderBy(member => member.MemberKey, StringComparer.Ordinal))
        {
            var path = new List<long>();
            var onPath = new HashSet<long>();
            long? current = start.MemberId;
            while (current is { } memberId && byId.ContainsKey(memberId) && !finished.Contains(memberId) && onPath.Add(memberId))
            {
                path.Add(memberId);
                current = byId[memberId].ParentMemberId;
            }

            if (current is { } repeated && onPath.Contains(repeated)) violations.Add(Cycle(path[path.IndexOf(repeated)..], byId));
            finished.UnionWith(path);
        }

        return violations;
    }

    /// <summary>A violation naming the members of a cycle in parent order, ending where it started.</summary>
    private static RuleViolation Cycle(List<long> cycle, Dictionary<long, DimensionMemberRecord> byId)
    {
        var keys = cycle.Append(cycle[0]).Select(memberId => $"'{byId[memberId].MemberKey}'");
        return new RuleViolation { Rule = Code, Message = $"Members {string.Join(" -> ", keys)} form a parent cycle." };
    }
}

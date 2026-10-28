using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>mapping_members_in_endpoint_versions</c>: every mapping's source member key names a member of the source dimension's live
/// version, and its target member key one of the target dimension's, because the active assumption set pins those versions and the
/// engine resolves the keys in them. An endpoint without a live version is <c>endpoint_versions_published</c>'s concern.
/// </summary>
public sealed class MappingMembersInEndpointVersionsRule : BridgeRule
{
    /// <summary><c>mapping_members_in_endpoint_versions</c>.</summary>
    protected override string Code => "mapping_members_in_endpoint_versions";

    /// <summary>Reports every mapping member the endpoint's live version lacks: the source, then the target of each mapping.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>Up to two violations per mapping.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        var source = snapshot.Surroundings.SourcePublished;
        var target = snapshot.Surroundings.TargetPublished;
        foreach (var mapping in snapshot.Mappings)
        {
            if (source is not null && !source.MemberKeys.Contains(mapping.SourceMemberKey))
                yield return OnMapping(mapping, $"Mapping {Describe(mapping)} has a source member that the live source dimension version lacks.");
            if (target is not null && !target.MemberKeys.Contains(mapping.TargetMemberKey))
                yield return OnMapping(mapping, $"Mapping {Describe(mapping)} has a target member that the live target dimension version lacks.");
        }
    }
}

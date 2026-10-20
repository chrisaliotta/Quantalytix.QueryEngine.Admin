using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>mapping_members_in_endpoint_versions</c>: every mapping's source member key names a member of the published version of
/// the bridge's source dimension, and its target member key one of the target dimension's, because the active assumption set
/// pins those versions and the engine resolves the keys in them. An endpoint with no published version is
/// <c>endpoint_versions_published</c>'s concern.
/// </summary>
public sealed class MappingMembersInEndpointVersionsRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "mapping_members_in_endpoint_versions";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports every mapping member key that its endpoint's published version lacks.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing source or target member.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var mapping in set.Mappings)
        {
            if (set.Source is { } source && !source.MemberKeys.Contains(mapping.SourceMemberKey))
            {
                var message = $"Mapping {BridgeMappingSet.Describe(mapping)} has a source member that the published source dimension version lacks.";
                violations.Add(new RuleViolation { Rule = Code, Message = message });
            }

            if (set.Target is { } target && !target.MemberKeys.Contains(mapping.TargetMemberKey))
            {
                var message = $"Mapping {BridgeMappingSet.Describe(mapping)} has a target member that the published target dimension version lacks.";
                violations.Add(new RuleViolation { Rule = Code, Message = message });
            }
        }

        return violations;
    }
}

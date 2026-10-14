using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>mapping_members_in_endpoint_versions</c>: every mapping's source member belongs to the source dimension version and
/// its target member to the target dimension version. Member IDs are global, so a foreign key alone cannot keep a mapping
/// inside the versions the bridge pins.
/// </summary>
public sealed class MappingMembersInEndpointVersionsRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "mapping_members_in_endpoint_versions";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports every mapping member outside its endpoint version.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per misplaced source or target member.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var mapping in set.Mappings)
        {
            if (!set.SourceMemberKeys.ContainsKey(mapping.SourceMemberId))
            {
                var message = $"Mapping {set.Describe(mapping)} has a source member that is not a member of the source dimension version.";
                violations.Add(new RuleViolation { Rule = Code, Message = message });
            }

            if (!set.TargetMemberKeys.ContainsKey(mapping.TargetMemberId))
            {
                var message = $"Mapping {set.Describe(mapping)} has a target member that is not a member of the target dimension version.";
                violations.Add(new RuleViolation { Rule = Code, Message = message });
            }
        }

        return violations;
    }
}

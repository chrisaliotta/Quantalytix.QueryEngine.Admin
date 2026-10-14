using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>cardinality_many_to_one</c>: with cardinality <c>many_to_one</c>, each source member maps to at most one target,
/// because a second target would count the source's facts twice in a cardinality the engine treats as additive.
/// </summary>
public sealed class CardinalityManyToOneRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "cardinality_many_to_one";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports every source member with several targets; other cardinalities have nothing to check.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per source member with several targets.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.Version.Cardinality != Cardinalities.ManyToOne) return [];
        return
        [
            .. set.Mappings
                .GroupBy(mapping => mapping.SourceMemberId)
                .Where(group => group.Count() > 1)
                .Select(group => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Source member {set.DescribeSource(group.Key)} maps to {group.Count()} targets ({set.DescribeTargets(group)}); "
                        + "cardinality many_to_one allows one."
                })
        ];
    }
}

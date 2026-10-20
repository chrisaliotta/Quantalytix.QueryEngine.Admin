using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The weight check that <c>allocation_partition</c> and <c>allocation_replicate</c> share: allocation weights are 0 or more.</summary>
internal static class NegativeWeights
{
    /// <summary>One violation per mapping whose allocation weight is below 0.</summary>
    /// <param name="set">The bridge version's mappings.</param>
    /// <param name="rule">The code of the rule that reports them.</param>
    /// <returns>The violations, in the set's mapping order.</returns>
    public static IEnumerable<RuleViolation> Find(BridgeMappingSet set, string rule)
    {
        return set.Mappings
            .Where(mapping => mapping.AllocationWeight < 0m)
            .Select(mapping => new RuleViolation
            {
                Rule = rule,
                Message = $"Mapping {BridgeMappingSet.Describe(mapping)} has allocation weight {DecimalText.Of(mapping.AllocationWeight)}; weights must be 0 or more."
            });
    }
}

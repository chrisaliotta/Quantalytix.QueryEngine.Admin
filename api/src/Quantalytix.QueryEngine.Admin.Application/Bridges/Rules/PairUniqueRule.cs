using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>pair_unique</c>: each source and target pair appears once per version (B6), because StarRocks does not enforce the declared
/// unique key and a repeated pair would count the source twice in the target.
/// </summary>
public sealed class PairUniqueRule : BridgeRule
{
    /// <summary><c>pair_unique</c>.</summary>
    protected override string Code => "pair_unique";

    /// <summary>Reports every mapping after the first that repeats a pair.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>One violation per repeating mapping.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        return snapshot.Mappings
            .GroupBy(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey))
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1).Select(mapping => OnMapping(mapping, $"Mapping {Describe(mapping)} appears {group.Count()} times.")));
    }
}

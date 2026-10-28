using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>cardinality_many_to_one</c>: with cardinality <c>many_to_one</c>, each source member maps to at most one target, because a second
/// target would count the source's facts twice in a cardinality the engine treats as additive.
/// </summary>
public sealed class CardinalityManyToOneRule : BridgeRule
{
    /// <summary><c>cardinality_many_to_one</c>.</summary>
    protected override string Code => "cardinality_many_to_one";

    /// <summary>Reports every source member with several targets; other cardinalities have nothing to check.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>One violation per such source member.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        if (snapshot.Version.Cardinality != Cardinalities.ManyToOne) return [];
        return BySource(snapshot)
            .Where(group => group.Count() > 1)
            .Select(group => OnSource(group.Key, $"Source member '{group.Key}' maps to {group.Count()} targets ({DescribeTargets(group)}); cardinality many_to_one allows one."));
    }
}

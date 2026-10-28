using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>allocation_none</c>: with allocation mode <c>none</c>, each source member maps to at most one target with an allocation weight of
/// exactly 1 (B5), because the source's full value moves to that one target. Under <c>many_to_one</c>, the one-target half is
/// <c>cardinality_many_to_one</c>'s finding, so a source is not reported twice.
/// </summary>
public sealed class AllocationNoneRule : BridgeRule
{
    /// <summary><c>allocation_none</c>.</summary>
    protected override string Code => "allocation_none";

    /// <summary>Reports source members with several targets, then mappings whose weight is not 1; other modes have nothing to check.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>The violations.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        if (snapshot.Version.AllocationMode != AllocationModes.None) return [];
        IEnumerable<RuleViolation> severalTargets = snapshot.Version.Cardinality == Cardinalities.ManyToOne
            ? []
            : BySource(snapshot)
                .Where(group => group.Count() > 1)
                .Select(group => OnSource(group.Key, $"Source member '{group.Key}' maps to {group.Count()} targets ({DescribeTargets(group)}); allocation mode none allows one."));
        var weightsOtherThanOne = snapshot.Mappings
            .Where(mapping => mapping.AllocationWeight != 1m)
            .Select(mapping => OnMapping(mapping, $"Mapping {Describe(mapping)} has allocation weight {DecimalText.Of(mapping.AllocationWeight)}; allocation mode none requires exactly 1."));
        return [.. severalTargets, .. weightsOtherThanOne];
    }
}

using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>allocation_partition</c>: with allocation mode <c>partition</c>, weights are 0 or more and sum to exactly 1 per source member
/// (B5), so a partitioned source's value is split without loss or duplication. Sums are compared in decimal, which holds the stored
/// <c>DECIMAL(38, 12)</c> weights exactly.
/// </summary>
public sealed class AllocationPartitionRule : BridgeRule
{
    /// <summary><c>allocation_partition</c>.</summary>
    protected override string Code => "allocation_partition";

    /// <summary>Reports negative weights, then source members whose weights do not sum to 1; other modes have nothing to check.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>The violations.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        if (snapshot.Version.AllocationMode != AllocationModes.Partition) return [];
        var sumsOtherThanOne = BySource(snapshot)
            .Select(group => (SourceMemberKey: group.Key, Sum: group.Sum(mapping => mapping.AllocationWeight)))
            .Where(source => source.Sum != 1m)
            .Select(source => OnSource(
                source.SourceMemberKey,
                $"Source member '{source.SourceMemberKey}' has allocation weights summing to {DecimalText.Of(source.Sum)}; partition requires exactly 1."));
        return [.. NegativeWeights(snapshot), .. sumsOtherThanOne];
    }
}

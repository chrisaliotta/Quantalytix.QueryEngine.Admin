using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>allocation_partition</c>: with allocation mode <c>partition</c>, weights are 0 or more and sum to exactly 1 per
/// source member (schema section 18), so a partitioned source's value is split without loss or duplication. Sums are
/// compared in decimal, which holds the stored <c>DECIMAL(38, 12)</c> weights exactly.
/// </summary>
public sealed class AllocationPartitionRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "allocation_partition";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports every negative weight and every source member whose weights do not sum to 1; other modes have nothing to check.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations: negative weights first, then source members whose sum is not 1.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.Version.AllocationMode != AllocationModes.Partition) return [];
        var sumsOtherThanOne = set.Mappings
            .GroupBy(mapping => mapping.SourceMemberKey)
            .Select(group => (SourceMemberKey: group.Key, Sum: group.Sum(mapping => mapping.AllocationWeight)))
            .Where(source => source.Sum != 1m)
            .Select(source => new RuleViolation
            {
                Rule = Code,
                Message = $"Source member '{source.SourceMemberKey}' has allocation weights summing to {DecimalText.Of(source.Sum)}; "
                    + "partition requires exactly 1."
            });
        return [.. NegativeWeights.Find(set, Code), .. sumsOtherThanOne];
    }
}

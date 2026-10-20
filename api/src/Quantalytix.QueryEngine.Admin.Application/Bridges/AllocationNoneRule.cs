using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>allocation_none</c>: with allocation mode <c>none</c>, each source member maps to at most one target with an
/// allocation weight of exactly 1 (schema section 10), because the source's full value moves to that one target.
/// </summary>
public sealed class AllocationNoneRule(BridgeMappingSetReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "allocation_none";

    /// <summary>The kind the rule checks: bridge versions.</summary>
    public string Kind { get; } = VersionKinds.BridgeVersion;

    /// <summary>Reports every source member with several targets and every weight other than 1; other modes have nothing to check.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations: source members with several targets first, then mappings whose weight is not 1.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var set = await reader.ReadAsync(versionId, cancellationToken);
        if (set.Version.AllocationMode != AllocationModes.None) return [];
        var severalTargets = set.Mappings
            .GroupBy(mapping => mapping.SourceMemberKey)
            .Where(group => group.Count() > 1)
            .Select(group => new RuleViolation
            {
                Rule = Code,
                Message = $"Source member '{group.Key}' maps to {group.Count()} targets ({BridgeMappingSet.DescribeTargets(group)}); "
                    + "allocation mode none allows one."
            });
        var weightsOtherThanOne = set.Mappings
            .Where(mapping => mapping.AllocationWeight != 1m)
            .Select(mapping => new RuleViolation
            {
                Rule = Code,
                Message = $"Mapping {BridgeMappingSet.Describe(mapping)} has allocation weight {DecimalText.Of(mapping.AllocationWeight)}; "
                    + "allocation mode none requires exactly 1."
            });
        return [.. severalTargets, .. weightsOtherThanOne];
    }
}

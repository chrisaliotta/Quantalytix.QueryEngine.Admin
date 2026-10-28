using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// A bridge version rule over a snapshot. Every bridge rule is a commit rule: a preview reports it on the mapping or source member at
/// fault so the editor can flag it, and the commit is refused while one remains, because the version is live (B11). Every rule runs
/// for <c>check</c> and freeze. The snapshot carries what the rules read outside the version, so they read nothing.
/// </summary>
public abstract class BridgeRule : ISnapshotRule<BridgeSnapshot>
{
    /// <summary>Commit: a finding the editor flags, which refuses the commit.</summary>
    public RuleStage Stage => RuleStage.Commit;

    /// <summary>The rule code its violations carry.</summary>
    protected abstract string Code { get; }

    /// <summary>Checks the snapshot.</summary>
    /// <param name="snapshot">The bridge version, stored or pending.</param>
    /// <param name="cancellationToken">Unused; the rule reads nothing.</param>
    /// <returns>The violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(BridgeSnapshot snapshot, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RuleViolation>>([.. Evaluate(snapshot)]);
    }

    /// <summary>The violations of a snapshot.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>The violations, in mapping order.</returns>
    protected abstract IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot);

    /// <summary>A finding on one mapping.</summary>
    /// <param name="mapping">The mapping at fault.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation OnMapping(BridgeMappingRecord mapping, string message)
    {
        return MappingViolations.Of(Code, message, MappingAreas.Mappings, mapping.BridgeMappingId, filter: mapping.SourceMemberKey);
    }

    /// <summary>A finding on every mapping of one source member.</summary>
    /// <param name="sourceMemberKey">The source member at fault.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation OnSource(string sourceMemberKey, string message)
    {
        return MappingViolations.Of(Code, message, MappingAreas.Mappings, itemKey: sourceMemberKey, filter: sourceMemberKey);
    }

    /// <summary>A finding on the version as a whole.</summary>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation OnVersion(string message)
    {
        return MappingViolations.Of(Code, message, MappingAreas.Mappings);
    }

    /// <summary>The mappings grouped by source member, in mapping order.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>One group per source member.</returns>
    protected static IEnumerable<IGrouping<string, BridgeMappingRecord>> BySource(BridgeSnapshot snapshot)
    {
        return snapshot.Mappings.GroupBy(mapping => mapping.SourceMemberKey, StringComparer.Ordinal);
    }

    /// <summary>A mapping as messages name it, such as <c>'CA' -> 'WEST'</c>.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <returns>Its source and target members.</returns>
    protected static string Describe(BridgeMappingRecord mapping)
    {
        return $"'{mapping.SourceMemberKey}' -> '{mapping.TargetMemberKey}'";
    }

    /// <summary>The target members of some mappings as messages list them, such as <c>'EAST', 'WEST'</c>.</summary>
    /// <param name="mappings">Mappings of one source member.</param>
    /// <returns>The targets, comma-separated, in the mappings' order.</returns>
    protected static string DescribeTargets(IEnumerable<BridgeMappingRecord> mappings)
    {
        return string.Join(", ", mappings.Select(mapping => $"'{mapping.TargetMemberKey}'"));
    }

    /// <summary>One finding per mapping whose allocation weight is below 0, which partition and replicate share.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>The violations, in mapping order.</returns>
    protected IEnumerable<RuleViolation> NegativeWeights(BridgeSnapshot snapshot)
    {
        return snapshot.Mappings
            .Where(mapping => mapping.AllocationWeight < 0m)
            .Select(mapping => OnMapping(mapping, $"Mapping {Describe(mapping)} has allocation weight {DecimalText.Of(mapping.AllocationWeight)}; weights must be 0 or more."));
    }
}

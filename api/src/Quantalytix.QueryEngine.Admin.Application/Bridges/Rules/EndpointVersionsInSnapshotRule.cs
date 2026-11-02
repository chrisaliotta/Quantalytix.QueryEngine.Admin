using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>endpoint_versions_in_snapshot</c>: the bridge's source and target dimensions each have a version in the bridge version's
/// snapshot, because the engine resolves the mappings' member keys in them.
/// </summary>
public sealed class EndpointVersionsInSnapshotRule : BridgeRule
{
    /// <summary><c>endpoint_versions_in_snapshot</c>.</summary>
    protected override string Code => "endpoint_versions_in_snapshot";

    /// <summary>Reports each endpoint dimension without a version in the snapshot, source first.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>Up to two violations.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        var surroundings = snapshot.Surroundings;
        if (surroundings.SourceKeys is null)
            yield return OnVersion($"The source dimension '{surroundings.Source.DimensionKey}' has no version in this snapshot for the mappings to resolve in.");
        if (surroundings.TargetKeys is null)
            yield return OnVersion($"The target dimension '{surroundings.Target.DimensionKey}' has no version in this snapshot for the mappings to resolve in.");
    }
}

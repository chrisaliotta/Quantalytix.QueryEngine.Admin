using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// <c>endpoint_versions_published</c>: the bridge's source and target dimensions each have a live version, because the active
/// assumption set pins them and the engine resolves the mappings' member keys in them.
/// </summary>
public sealed class EndpointVersionsPublishedRule : BridgeRule
{
    /// <summary><c>endpoint_versions_published</c>.</summary>
    protected override string Code => "endpoint_versions_published";

    /// <summary>Reports each endpoint dimension without a live version, source first.</summary>
    /// <param name="snapshot">The bridge version.</param>
    /// <returns>Up to two violations.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(BridgeSnapshot snapshot)
    {
        var surroundings = snapshot.Surroundings;
        if (surroundings.SourcePublished is null)
            yield return OnVersion($"The source dimension '{surroundings.Source.DimensionKey}' has no live version for the mappings to resolve in.");
        if (surroundings.TargetPublished is null)
            yield return OnVersion($"The target dimension '{surroundings.Target.DimensionKey}' has no live version for the mappings to resolve in.");
    }
}

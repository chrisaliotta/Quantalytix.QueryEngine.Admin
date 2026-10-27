using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>bridge_binding_route</c>: a bridge binding remaps a binding of the version through a bridge that starts at the source
/// binding's dimension and ends at its own, and its chain of source bindings does not lead back to it.
/// </summary>
public sealed class BridgeBindingRouteRule : BindingRule
{
    /// <summary><c>bridge_binding_route</c>.</summary>
    protected override string Code => "bridge_binding_route";

    /// <summary>Reports every broken bridge route.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>The violations, by binding name.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var bindings = snapshot.Bindings.ToDictionary(binding => binding.DimensionBindingId);
        var bridges = snapshot.Surroundings.Bridges.ToDictionary(bridge => bridge.BridgeId);
        foreach (var binding in ByName(snapshot.Bindings.Where(binding => binding.BindingKind == BindingKinds.Bridge), names))
        {
            var name = names[binding.DimensionBindingId];
            var source = bindings.GetValueOrDefault(binding.SourceDimensionBindingId!.Value);
            if (source is null)
                yield return Violation(binding, names, $"Bridge binding '{name}' remaps binding {binding.SourceDimensionBindingId}, which is not a binding of this version.");

            if (!bridges.TryGetValue(binding.BridgeId!.Value, out var bridge))
            {
                yield return Violation(binding, names, $"Bridge binding '{name}' uses bridge {binding.BridgeId}, which does not exist.");
            }
            else
            {
                if (source is not null && bridge.SourceDimensionId != source.DimensionId)
                    yield return Violation(binding, names, $"Bridge binding '{name}' uses bridge '{bridge.BridgeKey}', which starts at another dimension than its source binding '{names[source.DimensionBindingId]}'.");
                if (bridge.TargetDimensionId != binding.DimensionId)
                    yield return Violation(binding, names, $"Bridge binding '{name}' uses bridge '{bridge.BridgeKey}', which ends at another dimension than the binding.");
            }

            if (ReachesItself(binding, bindings))
                yield return Violation(binding, names, $"Bridge binding '{name}' leads back to itself through its source bindings.");
        }
    }

    /// <summary>Whether a binding's chain of source bindings leads back to it.</summary>
    private static bool ReachesItself(DimensionBindingRecord start, Dictionary<long, DimensionBindingRecord> bindings)
    {
        var visited = new HashSet<long>();
        var current = start.SourceDimensionBindingId;
        while (current is { } bindingId && bindings.TryGetValue(bindingId, out var next) && visited.Add(bindingId))
        {
            if (bindingId == start.DimensionBindingId) return true;
            current = next.SourceDimensionBindingId;
        }

        return false;
    }
}

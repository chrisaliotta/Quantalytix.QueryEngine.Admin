using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires each bridge binding to describe a route the query engine can follow: its source binding is a binding of the
/// same version, its bridge leads from the source binding's dimension to the bridge binding's own dimension, and following
/// source bindings never leads back to the bridge binding, which the engine would follow forever.
/// </summary>
public sealed class BridgeBindingRouteRule(ITableGateway<DimensionBindingRecord> bindings, ITableGateway<BridgeRecord> bridges, BindingNames names)
    : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "bridge_binding_route";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each break in a bridge binding's route.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per foreign source binding, missing bridge, mismatched bridge end and route cycle.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionBindings = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken)).ToDictionary(binding => binding.DimensionBindingId);
        var named = await names.OfAsync(versionBindings.Values, cancellationToken);
        var bridged = versionBindings.Values
            .Where(binding => binding.BindingKind == BindingKinds.Bridge)
            .OrderBy(binding => named[binding.DimensionBindingId], StringComparer.Ordinal)
            .ToList();
        var bridgesById = (await bridges.ListAsync(new { BridgeId = bridged.Select(binding => binding.BridgeId!.Value).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(bridge => bridge.BridgeId);

        var violations = new List<RuleViolation>();
        foreach (var binding in bridged)
        {
            var name = named[binding.DimensionBindingId];
            var source = versionBindings.GetValueOrDefault(binding.SourceDimensionBindingId!.Value);
            if (source is null)
            {
                violations.Add(Violation($"Bridge binding '{name}' remaps binding {binding.SourceDimensionBindingId}, which is not a binding of this version."));
            }

            if (!bridgesById.TryGetValue(binding.BridgeId!.Value, out var bridge))
            {
                violations.Add(Violation($"Bridge binding '{name}' uses bridge {binding.BridgeId}, which does not exist."));
            }
            else
            {
                if (source is not null && bridge.SourceDimensionId != source.DimensionId)
                {
                    violations.Add(Violation($"Bridge binding '{name}' uses bridge '{bridge.BridgeKey}', which starts at another dimension than its source binding '{named[source.DimensionBindingId]}'."));
                }

                if (bridge.TargetDimensionId != binding.DimensionId)
                {
                    violations.Add(Violation($"Bridge binding '{name}' uses bridge '{bridge.BridgeKey}', which ends at another dimension than the binding."));
                }
            }

            if (ReachesItself(binding, versionBindings))
            {
                violations.Add(Violation($"Bridge binding '{name}' leads back to itself through its source bindings."));
            }
        }

        return violations;
    }

    /// <summary>Whether following source bindings from a binding comes back to it; a cycle that excludes it ends the walk.</summary>
    private static bool ReachesItself(DimensionBindingRecord start, Dictionary<long, DimensionBindingRecord> versionBindings)
    {
        var visited = new HashSet<long>();
        var current = start.SourceDimensionBindingId;
        while (current is { } dimensionBindingId && versionBindings.TryGetValue(dimensionBindingId, out var next) && visited.Add(dimensionBindingId))
        {
            if (dimensionBindingId == start.DimensionBindingId) return true;
            current = next.SourceDimensionBindingId;
        }

        return false;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

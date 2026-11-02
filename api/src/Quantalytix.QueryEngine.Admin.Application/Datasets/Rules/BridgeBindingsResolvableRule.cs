using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>bridge_bindings_resolvable</c>: the bridge of every bridge binding has a version in the snapshot, because the engine maps
/// members through it.
/// </summary>
public sealed class BridgeBindingsResolvableRule : BindingIdentityRule
{
    /// <summary><c>bridge_bindings_resolvable</c>.</summary>
    protected override string Code => "bridge_bindings_resolvable";

    /// <summary><c>bridge</c>.</summary>
    protected override string IdentityKind => "bridge";

    /// <summary>The bridge of a bridge binding.</summary>
    /// <param name="binding">A binding.</param>
    /// <returns>The bridge, or null for any other binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Bridge ? binding.BridgeId : null;
    }

    /// <summary>The bridges with a version in the snapshot.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <returns>The bridge ids.</returns>
    protected override IReadOnlySet<long> Held(DatasetSurroundings surroundings)
    {
        return surroundings.HeldBridgeIds;
    }

    /// <summary>A bridge's key.</summary>
    /// <param name="surroundings">What surrounds the dataset version.</param>
    /// <param name="identityId">The bridge.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(DatasetSurroundings surroundings, long identityId)
    {
        return surroundings.Bridges.Single(bridge => bridge.BridgeId == identityId).BridgeKey;
    }
}

using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bridge_bindings_resolvable</c>: the bridge of every bridge binding of a pinned dataset version has a published version, because
/// the engine maps rows through it.
/// </summary>
public sealed class BridgeBindingsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<BridgeRecord> bridges)
    : BindingIdentityRule(reader)
{
    /// <summary><c>bridge_bindings_resolvable</c>.</summary>
    protected override string Code => "bridge_bindings_resolvable";

    /// <summary>A bridge.</summary>
    protected override string Area => PinKinds.Bridge;

    /// <summary>The bridge of a bridge binding.</summary>
    /// <param name="binding">A pinned binding.</param>
    /// <returns>The bridge, or null for any other binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Bridge ? binding.BridgeId : null;
    }

    /// <summary>The bridges with a published version.</summary>
    /// <param name="identityIds">The bridges.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published ones.</returns>
    protected override async Task<IReadOnlySet<long>> PublishedAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await bridgeVersions.ListAsync(new { BridgeId = identityIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.BridgeId)
            .ToHashSet();
    }

    /// <summary>The bridges' keys.</summary>
    /// <param name="identityIds">The bridges.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each key by id.</returns>
    protected override async Task<IReadOnlyDictionary<long, string>> KeysAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await bridges.ListAsync(new { BridgeId = identityIds }, cancellationToken)).ToDictionary(row => row.BridgeId, row => row.BridgeKey);
    }

    /// <summary>Names the binding, its dataset and its bridge.</summary>
    /// <param name="pinned">The binding.</param>
    /// <param name="identityKey">The bridge's key.</param>
    /// <returns>The message.</returns>
    protected override string Message(PinnedBinding pinned, string identityKey)
    {
        return $"Binding '{pinned.Name}' of dataset '{pinned.DatasetKey}' maps through bridge '{identityKey}', which has no published version.";
    }
}

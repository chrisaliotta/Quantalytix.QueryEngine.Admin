using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The analytical bridge commands, which run on stored state, never inside a change set: create a bridge with an empty live version,
/// <c>many_to_one</c> / <c>none</c> (OI-27), and delete a bridge (B12); both advance the semantic revision. A report bridge is refused,
/// because only its report writes it (B9, <see cref="ReportBridges"/>).
/// </summary>
public sealed class BridgeCommands(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<DimensionRecord> dimensions,
    BridgeStore store,
    BridgeVersionRemover remover,
    BridgeSnapshotLoader loader,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>The title of the refusal of endpoints a bridge cannot join.</summary>
    private const string EndpointsTitle = "Endpoints cannot be bridged";

    /// <summary>Creates an analytical bridge between two analytical, non-time dimensions (B4) with its empty live version.</summary>
    /// <param name="input">The new bridge, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The bridge and its version, or the refusal of a missing dimension (400), endpoints B4 refuses or a taken key (409).</returns>
    public async Task<Outcome<BridgeCreated>> CreateAsync(CreateBridgeInput input, CancellationToken cancellationToken)
    {
        var endpoints = (await dimensions.ListAsync(new { DimensionId = new[] { input.SourceDimensionId, input.TargetDimensionId } }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId);
        var missing = new Dictionary<string, string[]>();
        if (!endpoints.ContainsKey(input.SourceDimensionId)) missing["sourceDimensionId"] = [$"Dimension {input.SourceDimensionId} does not exist."];
        if (!endpoints.ContainsKey(input.TargetDimensionId)) missing["targetDimensionId"] = [$"Dimension {input.TargetDimensionId} does not exist."];
        if (missing.Count > 0) return Outcome<BridgeCreated>.Refused(Refusal.Invalid(missing));
        if (EndpointsRefusal(endpoints[input.SourceDimensionId], endpoints[input.TargetDimensionId]) is { } refused) return Outcome<BridgeCreated>.Refused(refused);
        var keys = (await bridges.ListAsync(null, cancellationToken)).Select(bridge => bridge.BridgeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keys.Contains(input.Key)) return Outcome<BridgeCreated>.Refused(Refusal.Conflict("Key taken", $"Another bridge already uses the key '{input.Key}'."));

        var bridge = new BridgeRecord
        {
            BridgeId = ids.NextId(),
            BridgeKey = input.Key,
            DisplayName = input.DisplayName,
            SourceDimensionId = input.SourceDimensionId,
            TargetDimensionId = input.TargetDimensionId
        };
        var created = await store.CreateAsync(bridge, Cardinalities.ManyToOne, AllocationModes.None, input.IsEnabled, null, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<BridgeCreated>.Success(created);
    }

    /// <summary>Deletes an analytical bridge nothing blocks (B12): its live version with its mappings, then the row.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when deleted; otherwise why nothing was deleted.</returns>
    public async Task<Refusal?> DeleteAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadLiveAsync(bridgeId, cancellationToken);
        if (snapshot.Surroundings.Owner is { } owner) return BridgeRefusals.OwnedByReport(snapshot.Bridge, owner);
        var check = BridgeDeleteRules.Bridge(snapshot);
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        await remover.RemoveVersionsAsync([snapshot.VersionId], cancellationToken);
        await remover.RemoveIdentitiesAsync([bridgeId], cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>The conflict of endpoints an analytical bridge cannot join (B4), or null when it can.</summary>
    private static Refusal? EndpointsRefusal(DimensionRecord source, DimensionRecord target)
    {
        if (source.DimensionId == target.DimensionId)
            return Refusal.Conflict(EndpointsTitle, "A bridge joins two different dimensions.");
        if (source.DimensionKind == DimensionKinds.Time || target.DimensionKind == DimensionKinds.Time)
            return Refusal.Conflict(EndpointsTitle, "Time dimensions are not bridged; their periods come from the time binding.");
        if (source.DimensionPurpose == DimensionPurposes.Report || target.DimensionPurpose == DimensionPurposes.Report)
            return Refusal.Conflict(EndpointsTitle, "Report dimensions are bridged only by their report's sources, which create those bridges.");
        return null;
    }
}

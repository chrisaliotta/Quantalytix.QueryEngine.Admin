using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The analytical bridge change set (OI-01) behind <c>bridges/{bridgeId}/changes</c>: a report bridge's is refused (B9); the preview
/// flags the mappings that break a rule, and the commit is refused while any finding remains (B11).
/// </summary>
/// <param name="pipeline">Runs the bridge aggregate.</param>
/// <param name="owners">Reads whether a report owns the bridge.</param>
/// <param name="bridges">Reads the bridge.</param>
public sealed class BridgeChanges(
    ChangeSetPipeline<BridgeSnapshot, BridgeOperation, BridgeEditState> pipeline,
    BridgeOwnerReader owners,
    ITableGateway<BridgeRecord> bridges)
{
    /// <summary>Applies the operations in memory and writes nothing.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pending state and findings, or the refusal.</returns>
    public async Task<Outcome<ChangeSetPreview<BridgeEditState>>> PreviewAsync(long bridgeId, ChangeSetRequest<BridgeOperation> request, CancellationToken cancellationToken)
    {
        if (await OwnedByReportAsync(bridgeId, cancellationToken) is { } refusal) return Outcome<ChangeSetPreview<BridgeEditState>>.Refused(refusal);
        return await pipeline.PreviewAsync(bridgeId, request, cancellationToken);
    }

    /// <summary>Applies the operations to the bridge as stored now and writes the result when it breaks no rule.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored state and id map, or the refusal with nothing written.</returns>
    public async Task<Outcome<ChangeSetCommit<BridgeEditState>>> CommitAsync(long bridgeId, ChangeSetRequest<BridgeOperation> request, CancellationToken cancellationToken)
    {
        if (await OwnedByReportAsync(bridgeId, cancellationToken) is { } refusal) return Outcome<ChangeSetCommit<BridgeEditState>>.Refused(refusal);
        return await pipeline.CommitAsync(bridgeId, request, cancellationToken);
    }

    /// <summary>The refusal of a report bridge, or null for an analytical one.</summary>
    private async Task<Refusal?> OwnedByReportAsync(long bridgeId, CancellationToken cancellationToken)
    {
        if ((await owners.OwnersAsync([bridgeId], cancellationToken)).GetValueOrDefault(bridgeId) is not { } owner) return null;
        return BridgeRefusals.OwnedByReport(await bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken), owner);
    }
}

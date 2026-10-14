using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads, creates, edits and deletes bridges: the semantic remappings from one canonical dimension to another.</summary>
public sealed class BridgeService(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    BridgeVersionService versionService,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists every bridge.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bridges by key, ignoring case.</returns>
    public async Task<IReadOnlyList<BridgeRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await bridges.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.BridgeKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one bridge.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bridge.</returns>
    public Task<BridgeRecord> GetAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
    }

    /// <summary>Creates a bridge without versions; each version pins its two endpoint dimension versions, so none exists yet.</summary>
    /// <param name="input">The new bridge, already validated.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored bridge.</returns>
    public async Task<BridgeRecord> CreateAsync(NewBridgeInput input, CancellationToken cancellationToken)
    {
        var bridge = new BridgeRecord
        {
            BridgeId = ids.NextId(),
            BridgeKey = input.BridgeKey,
            DisplayName = input.DisplayName,
            SourceDimensionId = input.SourceDimensionId,
            TargetDimensionId = input.TargetDimensionId,
            IsEnabled = input.IsEnabled
        };
        await bridges.InsertAsync(bridge, cancellationToken);
        return bridge;
    }

    /// <summary>
    /// Edits a bridge's display name and availability. A change advances the semantic revision, because the query engine
    /// caches bridges with their availability.
    /// </summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The stored bridge.</returns>
    public async Task<BridgeRecord> UpdateAsync(long bridgeId, BridgeInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(bridgeId, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, IsEnabled = input.IsEnabled };
        await bridges.UpdateAsync(updated, cancellationToken);
        if (updated != current) await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a bridge whose versions are all drafts and that no binding uses: its drafts with their mappings first, then
    /// the bridge.
    /// </summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the bridge was deleted; otherwise the detail naming what blocks the delete, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var bridge = await GetAsync(bridgeId, cancellationToken);
        var bridgeVersions = await versions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        long[] versionIds = [.. bridgeVersions.Select(version => version.BridgeVersionId)];
        var blockers = new DeleteBlockers($"Bridge '{bridge.BridgeKey}'");
        blockers.AddNamed(
            "it has non-draft versions",
            bridgeVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        blockers.AddNamed("used by bindings", await versionService.BindingNamesAsync(versionIds, cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await versionService.RemoveAsync(versionIds, cancellationToken);
        await bridges.DeleteAsync(new { BridgeId = bridgeId }, cancellationToken);
        return null;
    }
}

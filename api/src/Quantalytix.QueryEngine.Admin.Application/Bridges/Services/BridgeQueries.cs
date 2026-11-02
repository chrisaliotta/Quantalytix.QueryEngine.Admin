using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The bridge reads, of analytical and report bridges alike: the list of the bridges the live snapshot holds, one bridge, a live or
/// frozen version's content and the delete check.
/// </summary>
public sealed class BridgeQueries(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportVersionRecord> reportVersions,
    MappingReferenceReader references,
    BridgeOwnerReader owners,
    BridgeSnapshotLoader loader,
    SnapshotRuleRunner<BridgeSnapshot> rules,
    LiveSnapshot live)
{
    /// <summary>Lists every bridge, those starting or ending at one dimension, or the one bridge of a report source.</summary>
    /// <param name="query">The filters, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items: analytical bridges first, then report bridges, each by key.</returns>
    public async Task<IReadOnlyList<BridgeListItem>> ListAsync(BridgeListQuery query, CancellationToken cancellationToken)
    {
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var versionsOf = (await versions.ListAsync(null, cancellationToken)).ToLookup(version => version.BridgeId);
        var listed = (await ListedAsync(query, liveSnapshotId, cancellationToken))
            .Where(bridge => versionsOf[bridge.BridgeId].Any(version => version.SnapshotId == liveSnapshotId))
            .ToList();
        long[] bridgeIds = [.. listed.Select(bridge => bridge.BridgeId)];
        var counts = (await mappings.ListAsync(new { BridgeVersionId = bridgeIds.SelectMany(bridgeId => versionsOf[bridgeId]).Select(version => version.BridgeVersionId).ToArray() }, cancellationToken))
            .GroupBy(mapping => mapping.BridgeVersionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownerOf = await owners.OwnersAsync(bridgeIds, liveSnapshotId, cancellationToken);
        var dimensions = await references.DimensionsAsync(listed.SelectMany(bridge => new[] { bridge.SourceDimensionId, bridge.TargetDimensionId }), liveSnapshotId, cancellationToken);
        var bindings = await references.BindingsAsync(
            binding => binding.BridgeId is { } bridgeId && bridgeIds.Contains(bridgeId),
            binding => binding.BridgeId!.Value,
            liveSnapshotId,
            cancellationToken);
        return
        [
            .. listed
                .Select(bridge =>
                {
                    var own = versionsOf[bridge.BridgeId].ToList();
                    var mappingCounts = own.ToDictionary(version => version.BridgeVersionId, version => counts.GetValueOrDefault(version.BridgeVersionId));
                    return BridgeReadModels.ListItem(
                        bridge,
                        ownerOf.GetValueOrDefault(bridge.BridgeId),
                        dimensions[bridge.SourceDimensionId],
                        dimensions[bridge.TargetDimensionId],
                        own,
                        liveSnapshotId,
                        mappingCounts,
                        [.. bindings[bridge.BridgeId]]);
                })
                .OrderBy(item => item.Owner is not null)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Reads one bridge.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list item.</returns>
    public async Task<BridgeListItem> DetailAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return BridgeReadModels.Detail(await loader.LoadLiveAsync(bridgeId, cancellationToken));
    }

    /// <summary>Reads a version's content with the findings of its settings and mappings.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content.</returns>
    public async Task<BridgeVersionContent> ContentAsync(long versionId, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadVersionAsync(versionId, cancellationToken);
        return BridgeReadModels.Content(snapshot, await rules.EvaluateAsync(snapshot, cancellationToken), TemporaryIds.PresentStored);
    }

    /// <summary>Checks whether a bridge can be deleted; a report bridge never can here.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> BridgeDeleteCheckAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return BridgeDeleteRules.Bridge(await loader.LoadLiveAsync(bridgeId, cancellationToken));
    }

    /// <summary>The bridges the filters select; a report's are those of its live version's sources.</summary>
    private async Task<IReadOnlyList<BridgeRecord>> ListedAsync(BridgeListQuery query, long liveSnapshotId, CancellationToken cancellationToken)
    {
        if (query.ReportId is { } reportId)
        {
            var report = (await reportVersions.ListAsync(new { ReportId = reportId, SnapshotId = liveSnapshotId }, cancellationToken)).Single();
            var owning = await sources.ListAsync(new { report.ReportVersionId, query.SourceKey }, cancellationToken);
            return await bridges.ListAsync(new { BridgeId = owning.Select(source => source.BridgeId).OfType<long>().ToArray() }, cancellationToken);
        }

        if (query.DimensionId is not { } dimensionId) return await bridges.ListAsync(null, cancellationToken);
        var from = await bridges.ListAsync(new { SourceDimensionId = dimensionId }, cancellationToken);
        var to = await bridges.ListAsync(new { TargetDimensionId = dimensionId }, cancellationToken);
        return [.. from.Concat(to).DistinctBy(bridge => bridge.BridgeId)];
    }
}

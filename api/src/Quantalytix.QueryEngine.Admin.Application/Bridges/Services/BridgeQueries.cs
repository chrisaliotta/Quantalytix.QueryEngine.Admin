using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The bridge reads: the list, the detail, version content, rule checks and delete checks, of analytical and report bridges alike.</summary>
public sealed class BridgeQueries(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    ITableGateway<ReportSourceRecord> sources,
    MappingReferenceReader references,
    BridgeOwnerReader owners,
    BridgeSnapshotLoader loader,
    SnapshotRuleRunner<BridgeSnapshot> rules,
    TimeProvider time)
{
    /// <summary>Lists every bridge, those starting or ending at one dimension, or the one bridge of a report source.</summary>
    /// <param name="query">The filters, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items: analytical bridges first, then report bridges, each by key.</returns>
    public async Task<IReadOnlyList<BridgeListItem>> ListAsync(BridgeListQuery query, CancellationToken cancellationToken)
    {
        var listed = await ListedAsync(query, cancellationToken);
        long[] bridgeIds = [.. listed.Select(bridge => bridge.BridgeId)];
        var versionsOf = (await versions.ListAsync(new { BridgeId = bridgeIds }, cancellationToken)).ToLookup(version => version.BridgeId);
        var counts = (await mappings.ListAsync(new { BridgeVersionId = versionsOf.SelectMany(group => group).Select(version => version.BridgeVersionId).ToArray() }, cancellationToken))
            .GroupBy(mapping => mapping.BridgeVersionId)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownerOf = await owners.OwnersAsync(bridgeIds, cancellationToken);
        var dimensions = await references.DimensionsAsync(listed.SelectMany(bridge => new[] { bridge.SourceDimensionId, bridge.TargetDimensionId }), cancellationToken);
        var bindings = await references.BindingsAsync(new { BridgeId = bridgeIds }, binding => binding.BridgeId!.Value, cancellationToken);
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
                        mappingCounts,
                        [.. bindings[bridge.BridgeId]]);
                })
                .OrderBy(item => item.Owner is not null)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Reads a bridge's detail.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The detail.</returns>
    public async Task<BridgeDetail> DetailAsync(long bridgeId, CancellationToken cancellationToken)
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

    /// <summary>Runs every bridge rule on a version and changes nothing (OI-30).</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report.</returns>
    public async Task<ValidationReport> CheckAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = await rules.EvaluateAsync(await loader.LoadVersionAsync(versionId, cancellationToken), cancellationToken);
        return new ValidationReport { VersionId = versionId, CheckedAt = time.GetUtcNow().UtcDateTime, Violations = violations };
    }

    /// <summary>Checks whether a bridge can be deleted; a report bridge never can here.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> BridgeDeleteCheckAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return BridgeDeleteRules.Bridge(await loader.LoadLiveAsync(bridgeId, cancellationToken));
    }

    /// <summary>Checks whether a version can be deleted.</summary>
    /// <param name="versionId">The bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The check.</returns>
    public async Task<DeleteCheck> VersionDeleteCheckAsync(long versionId, CancellationToken cancellationToken)
    {
        return BridgeDeleteRules.Version(await loader.LoadVersionAsync(versionId, cancellationToken));
    }

    /// <summary>The bridges the filters select.</summary>
    private async Task<IReadOnlyList<BridgeRecord>> ListedAsync(BridgeListQuery query, CancellationToken cancellationToken)
    {
        if (query.ReportId is { } reportId)
        {
            var owning = await sources.ListAsync(new { ReportId = reportId, query.SourceKey }, cancellationToken);
            return await bridges.ListAsync(new { BridgeId = owning.Select(source => source.BridgeId).OfType<long>().ToArray() }, cancellationToken);
        }

        if (query.DimensionId is not { } dimensionId) return await bridges.ListAsync(null, cancellationToken);
        var from = await bridges.ListAsync(new { SourceDimensionId = dimensionId }, cancellationToken);
        var to = await bridges.ListAsync(new { TargetDimensionId = dimensionId }, cancellationToken);
        return [.. from.Concat(to).DistinctBy(bridge => bridge.BridgeId)];
    }
}

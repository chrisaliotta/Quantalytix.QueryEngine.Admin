using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Reads, creates, edits and deletes bridges: the semantic remappings from one canonical dimension to another. A bridge into a
/// report dimension belongs to the report source that created it, which alone edits and deletes it.
/// </summary>
public sealed class BridgeService(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportRecord> reports,
    BindingNames bindingNames,
    BridgeVersionService versionService,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists bridges with their purpose and owner.</summary>
    /// <param name="query">The purpose to narrow the list to, if any.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The bridges by key, ignoring case.</returns>
    public async Task<IReadOnlyList<BridgeSummary>> ListAsync(BridgeListQuery query, CancellationToken cancellationToken)
    {
        var summaries = await SummariesAsync(await bridges.ListAsync(null, cancellationToken), cancellationToken);
        return
        [
            .. summaries.Where(summary => query.Purpose is null || summary.Purpose == query.Purpose)
                .OrderBy(summary => summary.Bridge.BridgeKey, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>Reads one bridge.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bridge.</returns>
    public Task<BridgeRecord> GetAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
    }

    /// <summary>Reads one bridge with its purpose and owner.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The bridge's summary.</returns>
    public async Task<BridgeSummary> SummaryAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return (await SummariesAsync([await GetAsync(bridgeId, cancellationToken)], cancellationToken)).Single();
    }

    /// <summary>Creates a bridge without versions; its mappings belong to versions, so none exists yet.</summary>
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
    /// Deletes a bridge whose versions are all drafts and that no binding uses: its drafts with their mappings first, then the
    /// bridge.
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
        var boundBy = await bindings.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        var bound = await bindingNames.OfAsync(boundBy, cancellationToken);
        blockers.AddNamed("used by bindings", boundBy.Select(binding => $"'{bound[binding.DimensionBindingId]}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await RemoveAsync(bridgeId, versionIds, cancellationToken);
        return null;
    }

    /// <summary>
    /// Deletes a bridge that nothing uses any more, with every version and its mappings, then the bridge. A report source removes
    /// its own bridge this way when it is deleted.
    /// </summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="versionIds">Every version of the bridge.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the bridge is gone.</returns>
    public async Task RemoveAsync(long bridgeId, IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await versionService.RemoveAsync(versionIds, cancellationToken);
        await bridges.DeleteAsync(new { BridgeId = bridgeId }, cancellationToken);
    }

    /// <summary>
    /// Gives bridges their purpose, the purpose of their target dimension, and report bridges the report source that owns them.
    /// </summary>
    private async Task<IReadOnlyList<BridgeSummary>> SummariesAsync(IReadOnlyList<BridgeRecord> rows, CancellationToken cancellationToken)
    {
        var reportDimensionIds = (await dimensions.ListAsync(new { DimensionPurpose = DimensionPurposes.Report }, cancellationToken))
            .Select(dimension => dimension.DimensionId)
            .ToHashSet();
        var owners = (await sources.ListAsync(new { BridgeId = rows.Select(row => row.BridgeId).ToArray() }, cancellationToken))
            .ToDictionary(source => source.BridgeId!.Value);
        var reportKeys = (await reports.ListAsync(new { ReportId = owners.Values.Select(source => source.ReportId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        return
        [
            .. rows.Select(row => new BridgeSummary
            {
                Bridge = row,
                Purpose = reportDimensionIds.Contains(row.TargetDimensionId) ? DimensionPurposes.Report : DimensionPurposes.Analytical,
                Owner = owners.TryGetValue(row.BridgeId, out var source)
                    ? new BridgeOwner { ReportId = source.ReportId, ReportKey = reportKeys[source.ReportId], ReportSourceId = source.ReportSourceId, SourceKey = source.SourceKey }
                    : null
            })
        ];
    }
}

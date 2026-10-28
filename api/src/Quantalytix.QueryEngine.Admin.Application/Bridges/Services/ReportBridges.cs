using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The writes of report bridges, which this module reads but only the reports module makes (B9), through its report editor. A report
/// bridge maps one report source's dimension into the report's dimension: <c>many_to_many</c> and <c>replicate</c>, with one live
/// version edited in place, whose mappings are the source's rules: each adds an item, or every item under it, into one row (a target
/// member) with its factor and the whole weight. The methods write at once and leave the semantic revision to the report's change,
/// except <see cref="CreateAsync"/>, whose publish advances it.
/// </summary>
public sealed class ReportBridges(
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> versions,
    ITableGateway<BridgeMappingRecord> mappings,
    BridgeStore store,
    IIdGenerator ids)
{
    /// <summary>Creates a report source's bridge with its live version 1, empty or with a copy of another report bridge's rules.</summary>
    /// <param name="input">The new bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new bridge's id, which the source stores as its <c>bridge_id</c>.</returns>
    public async Task<long> CreateAsync(ReportBridgeInput input, CancellationToken cancellationToken)
    {
        var bridge = new BridgeRecord
        {
            BridgeId = ids.NextId(),
            BridgeKey = input.Key,
            DisplayName = input.DisplayName,
            SourceDimensionId = input.SourceDimensionId,
            TargetDimensionId = input.ReportDimensionId,
            IsEnabled = true
        };
        long? template = input.CopyFromBridgeId is { } copyFrom ? (await LiveAsync(copyFrom, cancellationToken)).BridgeVersionId : null;
        return (await store.CreateAsync(bridge, Cardinalities.ManyToMany, AllocationModes.Replicate, template, cancellationToken)).BridgeId;
    }

    /// <summary>Gives a report bridge its source's new display name.</summary>
    /// <param name="bridgeId">The report bridge.</param>
    /// <param name="displayName">The source's display name.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>A task that completes when the bridge is stored.</returns>
    public async Task RenameAsync(long bridgeId, string displayName, CancellationToken cancellationToken)
    {
        var bridge = await bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
        await bridges.UpdateAsync(bridge with { DisplayName = displayName }, cancellationToken);
    }

    /// <summary>Replaces the rules that add into one row; the rules of the source's other rows stay.</summary>
    /// <param name="bridgeId">The report bridge.</param>
    /// <param name="rowKey">The row: a member of the report's dimension.</param>
    /// <param name="rules">The row's complete rule list, validated by the report editor.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the rules are stored.</returns>
    public async Task ReplaceRowAsync(long bridgeId, string rowKey, IReadOnlyList<ReportBridgeRule> rules, CancellationToken cancellationToken)
    {
        var live = await LiveAsync(bridgeId, cancellationToken);
        await mappings.DeleteAsync(new { live.BridgeVersionId, TargetMemberKey = rowKey }, cancellationToken);
        await mappings.InsertManyAsync(
            [
                .. rules.Select(rule => new BridgeMappingRecord
                {
                    BridgeMappingId = ids.NextId(),
                    BridgeVersionId = live.BridgeVersionId,
                    SourceMemberKey = rule.ItemKey,
                    TargetMemberKey = rowKey,
                    Factor = rule.Factor,
                    AllocationWeight = 1m,
                    SourceSelection = rule.Selection
                })
            ],
            cancellationToken);
    }

    /// <summary>Removes the rules that add into rows the report no longer has.</summary>
    /// <param name="bridgeId">The report bridge.</param>
    /// <param name="rowKeys">The removed rows.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>A task that completes when the rules are deleted.</returns>
    public async Task RemoveRowsAsync(long bridgeId, IReadOnlyCollection<string> rowKeys, CancellationToken cancellationToken)
    {
        var live = await LiveAsync(bridgeId, cancellationToken);
        await mappings.DeleteAsync(new { live.BridgeVersionId, TargetMemberKey = rowKeys.ToArray() }, cancellationToken);
    }

    /// <summary>Deletes a report source's bridge with its source: its pins, its version with the rules, then the bridge.</summary>
    /// <param name="bridgeId">The report bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the bridge is gone.</returns>
    public Task DeleteAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return store.RemoveAsync(bridgeId, cancellationToken);
    }

    /// <summary>The live version of a report bridge, its only one.</summary>
    private async Task<BridgeVersionRecord> LiveAsync(long bridgeId, CancellationToken cancellationToken)
    {
        return (await versions.ListAsync(new { BridgeId = bridgeId, Status = PublicationStatuses.Published }, cancellationToken)).Single();
    }
}

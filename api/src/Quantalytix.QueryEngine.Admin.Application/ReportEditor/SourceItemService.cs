using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Replaces the items of one bridge source that map to one row. The items are entries of the source's bridge, which maps each item
/// to at most one row, so an item listed for the row leaves the row it mapped to before.
/// </summary>
public sealed class SourceItemService(ITableGateway<ReportSourceRecord> sources, BridgeMappingService bridgeMappings, OwnedVersions owned)
{
    /// <summary>Replaces a row's items in a bridge source.</summary>
    /// <param name="sourceId">A bridge source.</param>
    /// <param name="input">The row and its complete, validated item list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the entries are written.</returns>
    public async Task ReplaceAsync(long sourceId, SourceItemsInput input, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        var bridgeVersionId = (await owned.BridgesAsync([source], cancellationToken))[sourceId].BridgeVersionId;
        var entries = await bridgeMappings.ListAsync(bridgeVersionId, cancellationToken);
        var moved = input.Items.Select(item => item.ItemKey).ToHashSet(StringComparer.Ordinal);
        BridgeMappingInput[] replaced =
        [
            .. entries.Where(entry => entry.TargetMemberKey != input.RowKey && !moved.Contains(entry.SourceMemberKey)).Select(BridgeEntries.Kept),
            .. input.Items.Select(item => BridgeEntries.ToRow(item.ItemKey, input.RowKey, item.Factor))
        ];
        await bridgeMappings.ReplaceAsync(bridgeVersionId, new BridgeMappingsInput { Mappings = replaced }, cancellationToken);
    }
}

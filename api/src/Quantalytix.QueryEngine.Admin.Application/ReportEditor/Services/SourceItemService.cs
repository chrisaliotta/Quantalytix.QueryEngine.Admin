using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Replaces the rules of one bridge source that add into one row. The rules are entries of the source's bridge, which replicates
/// each item into every row a rule names, so the source's other rows keep their rules: an item may add into several rows, whole or
/// in part. A rule's factor reaches the report only through measures that apply bridge factors, which the report rules check.
/// </summary>
public sealed class SourceItemService(
    ITableGateway<ReportSourceRecord> sources,
    BridgeMappingService bridgeMappings,
    OwnedVersions owned,
    ReportChanges changes)
{
    /// <summary>Replaces a row's rules in a bridge source.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceId">A bridge source of the report.</param>
    /// <param name="input">The row and its complete, validated rule list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the rules, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, long sourceId, SourceItemsInput input, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        var bridgeVersionId = (await owned.BridgesAsync([source], cancellationToken))[sourceId].BridgeVersionId;
        var entries = await bridgeMappings.ListAsync(bridgeVersionId, cancellationToken);
        BridgeMappingInput[] replaced =
        [
            .. entries.Where(entry => entry.TargetMemberKey != input.RowKey).Select(BridgeEntries.Kept),
            .. input.Items.Select(item => BridgeEntries.ToRow(item, input.RowKey))
        ];
        ReportSourceItem[] rowItems =
        [
            .. input.Items.Select(item => new ReportSourceItem
            {
                ReportSourceId = sourceId,
                ItemKey = item.ItemKey,
                Selection = item.Selection,
                RowKey = input.RowKey,
                Factor = item.Factor
            })
        ];
        return await changes.ApplyAsync(
            reportId,
            report => report with
            {
                SourceItems = [.. report.SourceItems.Where(item => item.ReportSourceId != sourceId || item.RowKey != input.RowKey), .. rowItems]
            },
            () => bridgeMappings.ReplaceAsync(bridgeVersionId, new BridgeMappingsInput { Mappings = replaced }, cancellationToken),
            cancellationToken);
    }
}

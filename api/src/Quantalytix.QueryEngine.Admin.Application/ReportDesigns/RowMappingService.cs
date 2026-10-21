using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Replaces the source items that map to one row of a report design. The items are entries of the paired versions of the design
/// sources' bridges, which map each item to at most one row, so an item listed for the row leaves the row it mapped to before.
/// </summary>
public sealed class RowMappingService(
    ReportDesignVersions designs,
    ReportDesignReader reader,
    BridgeMappingService bridgeMappings,
    ITableGateway<VariableSourceRecord> sources)
{
    /// <summary>Replaces a row's items in every design source of the version.</summary>
    /// <param name="reportVersionId">A draft or published report version.</param>
    /// <param name="input">The row and its complete, validated item list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The design afterwards.</returns>
    public async Task<ReportDesign> ReplaceAsync(long reportVersionId, RowMappingsInput input, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var used = (await sources.ListAsync(new { ReportVersionId = reportVersionId, ClassificationKind = ClassificationKinds.Bridge }, cancellationToken))
            .Select(source => source.BridgeId!.Value)
            .ToHashSet();
        var listed = input.Mappings.ToLookup(mapping => mapping.BridgeId);
        foreach (var bridgeVersion in scope.BridgeVersions.Values.Where(version => used.Contains(version.BridgeId)))
        {
            var entries = await bridgeMappings.ListAsync(bridgeVersion.BridgeVersionId, cancellationToken);
            var rowItems = listed[bridgeVersion.BridgeId].ToList();
            var moved = rowItems.Select(mapping => mapping.ItemKey).ToHashSet(StringComparer.Ordinal);
            var others = entries.Where(entry => entry.TargetMemberKey != input.RowKey && !moved.Contains(entry.SourceMemberKey)).ToList();
            if (others.Count + rowItems.Count == entries.Count && rowItems.All(item => Unchanged(entries, input.RowKey, item))) continue;

            BridgeMappingInput[] replaced =
            [
                .. others.Select(BridgeEntries.Kept),
                .. rowItems.Select(item => BridgeEntries.ToRow(item.ItemKey, input.RowKey, item.Factor))
            ];
            await bridgeMappings.ReplaceAsync(bridgeVersion.BridgeVersionId, new BridgeMappingsInput { Mappings = replaced }, cancellationToken);
        }
        return await reader.ReadAsync(reportVersionId, cancellationToken);
    }

    /// <summary>Whether a listed item already maps to the row with the same factor.</summary>
    private static bool Unchanged(IReadOnlyList<BridgeMappingRecord> entries, string rowKey, RowMappingInput item)
    {
        return entries.Any(entry => entry.TargetMemberKey == rowKey && entry.SourceMemberKey == item.ItemKey && entry.Factor == item.Factor);
    }
}

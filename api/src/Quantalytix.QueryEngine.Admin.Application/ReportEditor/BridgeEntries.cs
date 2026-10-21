using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>The bridge entries the report editor writes back when it replaces a bridge version's mapping list.</summary>
internal static class BridgeEntries
{
    /// <summary>A stored entry as the mapping list takes it back.</summary>
    /// <param name="entry">A stored bridge mapping.</param>
    /// <returns>The same mapping as input.</returns>
    public static BridgeMappingInput Kept(BridgeMappingRecord entry)
    {
        return new BridgeMappingInput
        {
            SourceMemberKey = entry.SourceMemberKey,
            TargetMemberKey = entry.TargetMemberKey,
            Factor = entry.Factor,
            AllocationWeight = entry.AllocationWeight,
            SourceSelection = entry.SourceSelection
        };
    }

    /// <summary>
    /// The entry of a rule that adds an item, or every item under it, into a row with its whole weight: a report source's bridge
    /// replicates its items into every row a rule names, and the rule's factor scales them.
    /// </summary>
    /// <param name="item">The rule.</param>
    /// <param name="rowKey">The member of the report dimension.</param>
    /// <returns>The mapping input.</returns>
    public static BridgeMappingInput ToRow(SourceItemInput item, string rowKey)
    {
        return new BridgeMappingInput
        {
            SourceMemberKey = item.ItemKey,
            TargetMemberKey = rowKey,
            Factor = item.Factor,
            AllocationWeight = 1m,
            SourceSelection = item.Selection
        };
    }
}

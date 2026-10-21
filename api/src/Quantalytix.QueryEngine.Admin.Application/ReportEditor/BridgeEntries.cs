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
            AllocationWeight = entry.AllocationWeight
        };
    }

    /// <summary>An entry that maps one item to one row with its whole weight, as every bridge source's bridge does.</summary>
    /// <param name="itemKey">The member of the item dimension.</param>
    /// <param name="rowKey">The member of the report dimension.</param>
    /// <param name="factor">The signed coefficient.</param>
    /// <returns>The mapping input.</returns>
    public static BridgeMappingInput ToRow(string itemKey, string rowKey, decimal factor)
    {
        return new BridgeMappingInput { SourceMemberKey = itemKey, TargetMemberKey = rowKey, Factor = factor, AllocationWeight = 1m };
    }
}

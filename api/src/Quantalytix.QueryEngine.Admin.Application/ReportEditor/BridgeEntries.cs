using Quantalytix.QueryEngine.Admin.Application.Bridges;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>The rules the report editor writes into a bridge source's bridge.</summary>
internal static class BridgeEntries
{
    /// <summary>
    /// The rule of an item that adds the item, or every item under it, into a row: a report source's bridge replicates its items into
    /// every row a rule names, and the rule's factor scales them.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The rule.</returns>
    public static ReportBridgeRule Rule(SourceItemInput item)
    {
        return new ReportBridgeRule { ItemKey = item.ItemKey, Selection = item.Selection, Factor = item.Factor };
    }
}

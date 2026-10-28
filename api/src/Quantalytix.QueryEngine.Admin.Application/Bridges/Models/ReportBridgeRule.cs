namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>One rule of a report row, for <see cref="ReportBridges.ReplaceRowAsync"/>: an item of the source, or every item under it, added into the row.</summary>
public sealed record ReportBridgeRule
{
    /// <summary>The key of the source dimension's member.</summary>
    public required string ItemKey { get; init; }

    /// <summary>A <c>source_selection</c> value: the member alone, or with every member under it (B8).</summary>
    public required string Selection { get; init; }

    /// <summary>The coefficient the item's values take in the row, such as <c>-1</c> to take them back out.</summary>
    public required decimal Factor { get; init; }
}

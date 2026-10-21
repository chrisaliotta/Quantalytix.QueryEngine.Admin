namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One rule of a bridge source, as the report rules read it: an entry of the source's bridge that adds into a row.</summary>
public sealed record ReportSourceItem
{
    /// <summary>The bridge source.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The key of the member of the source's item dimension the rule takes.</summary>
    public required string ItemKey { get; init; }

    /// <summary>A <c>source_selection</c> value: the member alone, or the member and every member under it.</summary>
    public required string Selection { get; init; }

    /// <summary>The row the rule adds into.</summary>
    public required string RowKey { get; init; }

    /// <summary>The coefficient the rule's values take in the row.</summary>
    public required decimal Factor { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One rule of a bridge source that adds into a row: an entry of the source's bridge.</summary>
public sealed record EditorRowItem
{
    /// <summary>The bridge source.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The key of the member of the source's item dimension the rule takes.</summary>
    public required string ItemKey { get; init; }

    /// <summary>A <c>source_selection</c> value: the member alone, or the member and every member under it.</summary>
    public required string Selection { get; init; }

    /// <summary>The coefficient the rule's values take in the row, such as 0.5 to take half of them or -1 to take them back out.</summary>
    public required decimal Factor { get; init; }
}

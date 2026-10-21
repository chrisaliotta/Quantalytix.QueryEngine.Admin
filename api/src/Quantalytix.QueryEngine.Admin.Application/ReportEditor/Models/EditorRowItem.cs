namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One item of a bridge source that maps to a row: an entry of the source's bridge.</summary>
public sealed record EditorRowItem
{
    /// <summary>The bridge source.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The key of the member of the source's item dimension that maps to the row.</summary>
    public required string ItemKey { get; init; }

    /// <summary>The signed coefficient the item's values take in the row, such as -1 to flip a sign.</summary>
    public required decimal Factor { get; init; }
}

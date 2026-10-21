namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Every item of one bridge source that maps to one row, as <c>PUT reports/{reportId}/sources/{sourceId}/items</c> takes them. The
/// row key travels in the body because member keys may hold characters a path segment cannot.
/// </summary>
public sealed record SourceItemsInput
{
    /// <summary>The data row the items map to.</summary>
    public required string RowKey { get; init; }

    /// <summary>The row's complete item list: the row's entries the list leaves out are deleted, and a listed item mapped to another row moves here.</summary>
    public required IReadOnlyList<SourceItemInput> Items { get; init; }
}

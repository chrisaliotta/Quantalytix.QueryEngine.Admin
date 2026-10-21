namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Every rule of one bridge source that adds into one row, as <c>PUT reports/{reportId}/sources/{sourceId}/items</c> takes them.
/// The row key travels in the body because member keys may hold characters a path segment cannot.
/// </summary>
public sealed record SourceItemsInput
{
    /// <summary>The data row the rules add into.</summary>
    public required string RowKey { get; init; }

    /// <summary>The row's complete rule list: the row's rules the list leaves out are deleted, and the source's other rows keep theirs.</summary>
    public required IReadOnlyList<SourceItemInput> Items { get; init; }
}

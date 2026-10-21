namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>A report's complete row tree, as <c>PUT reports/{reportId}/rows</c> takes it.</summary>
public sealed record ReportRowsInput
{
    /// <summary>Every row; a stored row the list leaves out is deleted, and a key the report does not hold yet is a new row.</summary>
    public required IReadOnlyList<ReportRowInput> Rows { get; init; }
}

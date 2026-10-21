namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One row of a report's complete row tree.</summary>
public sealed record ReportRowInput
{
    /// <summary>The row's key, fixed once created: a stored row keeps its key, and a new key is a new row.</summary>
    public required string Key { get; init; }

    /// <summary>The label the report shows.</summary>
    public required string Label { get; init; }

    /// <summary>A <c>report_row_kind</c> value; a stored member row stays a member row and a stored overlay row an overlay row.</summary>
    public required string Kind { get; init; }

    /// <summary>The key of the row it sits under, which the list holds; null for the top level.</summary>
    public string? ParentKey { get; init; }

    /// <summary>Order among its siblings; a member comes before an overlay row on a tie.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether a group row shows its total; false makes it a header. Every other kind shows its values.</summary>
    public required bool ShowsValues { get; init; }

    /// <summary>The calculated measure of the model's published version that a formula row evaluates; null for the other kinds.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the report's column a formula row places its value in; null for the other kinds.</summary>
    public string? ValueColumnKey { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>One row of a report: a member of the report dimension or an overlay row.</summary>
public sealed record EditorRow
{
    /// <summary>The member or overlay row key, which requests, direct sources' field values and bridge entries name it by.</summary>
    public required string Key { get; init; }

    /// <summary>The label the report shows.</summary>
    public required string Label { get; init; }

    /// <summary>A <c>report_row_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The key of the row it sits under; null for the top level.</summary>
    public string? ParentKey { get; init; }

    /// <summary>Order among its siblings; a member comes before an overlay row on a tie.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether a group row shows its total; false makes it a header. Always true for the other kinds.</summary>
    public required bool ShowsValues { get; init; }

    /// <summary>The calculated measure a formula row evaluates; null for the other kinds.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the column a formula row places its value in; null for the other kinds.</summary>
    public string? ValueColumnKey { get; init; }

    /// <summary>The items the report's bridge sources map to the row, by source, then item key.</summary>
    public required IReadOnlyList<EditorRowItem> Items { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One column of a report's complete, ordered column list.</summary>
public sealed record ReportColumnInput
{
    /// <summary>The column's ID when it already exists, which keeps the formula rows that place values in it; null for a new column.</summary>
    public long? ReportColumnId { get; init; }

    /// <summary>Key, unique among the report's columns; <c>line</c> and <c>period</c> are reserved.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>A label replacing the metric's own; null shows the metric's.</summary>
    public string? DisplayName { get; init; }

    /// <summary>A <c>report_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The variable or calculated measure key of the published version of the report's model that the column returns.</summary>
    public required string MetricKey { get; init; }

    /// <summary>A presentation hint such as <c>percent</c> replacing the metric's own; null shows the metric's.</summary>
    public string? DisplayFormat { get; init; }

    /// <summary>Whether a request without columns returns the column.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}

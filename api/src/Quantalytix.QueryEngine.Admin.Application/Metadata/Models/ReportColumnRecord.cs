namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_column</c> row: one returned field of a report: a model variable or calculated measure.</summary>
public sealed record ReportColumnRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportColumnId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Key, unique among the report's columns.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>A label replacing the metric's own; null shows the metric's.</summary>
    public string? DisplayName { get; init; }

    /// <summary>A <c>report_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The key of the model's variable or calculated measure the column shows, according to its value kind.</summary>
    public required string MetricKey { get; init; }

    /// <summary>Order among the report's columns.</summary>
    public required int SortOrder { get; init; }

    /// <summary>A presentation hint such as <c>percent</c> replacing the metric's own; null shows the metric's.</summary>
    public string? DisplayFormat { get; init; }

    /// <summary>Whether a request without columns returns the column.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}

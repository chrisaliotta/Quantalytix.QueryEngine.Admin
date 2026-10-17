namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One column of a report version's complete, ordered column list.</summary>
public sealed record ReportColumnInput
{
    /// <summary>The column's ID when it already exists, which keeps the formula lines that place values in it; null for a new column.</summary>
    public long? ReportColumnId { get; init; }

    /// <summary>Key, unique among the version's columns; <c>line</c> and <c>period</c> are reserved.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The variable or calculated measure key of the report's model version that the column returns.</summary>
    public required string MetricKey { get; init; }

    /// <summary>Presentation hint such as <c>number</c>, <c>percent</c> or <c>currency</c>.</summary>
    public string? DisplayFormat { get; init; }

    /// <summary>Whether a request without columns returns the column.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}

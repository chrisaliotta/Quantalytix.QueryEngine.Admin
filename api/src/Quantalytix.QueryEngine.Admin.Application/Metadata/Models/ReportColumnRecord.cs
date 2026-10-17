namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_column</c> row: one returned field of a report version: a model variable or calculated measure.</summary>
public sealed record ReportColumnRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportColumnId { get; init; }

    /// <summary>The owning report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>Key, unique among the version's columns.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_value_kind</c> value.</summary>
    public required string ValueKind { get; init; }

    /// <summary>The variable of a <c>variable</c> column.</summary>
    public long? VariableId { get; init; }

    /// <summary>The calculated measure of a <c>calculated</c> column.</summary>
    public long? CalculatedMeasureId { get; init; }

    /// <summary>Order among the version's columns.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Presentation hint such as <c>number</c>, <c>percent</c> or <c>currency</c>.</summary>
    public string? DisplayFormat { get; init; }

    /// <summary>Whether a request without columns returns the column.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }
}

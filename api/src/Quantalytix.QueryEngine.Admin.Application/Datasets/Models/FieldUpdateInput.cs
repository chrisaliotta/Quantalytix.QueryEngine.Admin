namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The editable attributes of a field. <see cref="FieldInput"/> adds the key, which is fixed at creation. The materialized
/// column is set by its own endpoints.
/// </summary>
public record FieldUpdateInput
{
    /// <summary>Human-readable field name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>field_source_kind</c> value: <c>column</c>, <c>json_path</c> or <c>expression</c>.</summary>
    public required string FieldSourceKind { get; init; }

    /// <summary>The physical column a <c>column</c> field reads, or the JSON column of a <c>json_path</c> field.</summary>
    public string? SourceColumn { get; init; }

    /// <summary>The JSON path of a <c>json_path</c> field, such as <c>$.address.state</c>.</summary>
    public string? JsonPath { get; init; }

    /// <summary>The row expression of an <c>expression</c> field, such as <c>[f.balance] * [f.rate]</c>.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>Decimal precision (1-38); required for <c>decimal</c> and null otherwise.</summary>
    public int? NumericPrecision { get; init; }

    /// <summary>Decimal scale (0 up to the precision); required for <c>decimal</c> and null otherwise.</summary>
    public int? NumericScale { get; init; }

    /// <summary>Whether the logical field may be null.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>Optional semantic unit such as <c>USD</c>, <c>percent</c> or <c>days</c>.</summary>
    public string? UnitKey { get; init; }

    /// <summary>Whether the field may appear in detail projection.</summary>
    public required bool IsSelectable { get; init; }

    /// <summary>Whether the field may be referenced in source-row filters.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Whether the field may take part in final ordering.</summary>
    public required bool IsSortable { get; init; }

    /// <summary>Whether the field may be used directly as an aggregate grouping dimension.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}

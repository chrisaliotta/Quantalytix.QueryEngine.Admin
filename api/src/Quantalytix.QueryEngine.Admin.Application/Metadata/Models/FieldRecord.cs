namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_field</c> row: a logical field within an immutable dataset version.</summary>
public sealed record FieldRecord
{
    /// <summary>Surrogate primary key.</summary>
    public required long FieldId { get; init; }

    /// <summary>The owning dataset version.</summary>
    public required long DatasetVersionId { get; init; }

    /// <summary>Stable logical field identity; the request-facing grouping ID.</summary>
    public required string FieldKey { get; init; }

    /// <summary>Human-readable field name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>field_source_kind</c> value.</summary>
    public required string FieldSourceKind { get; init; }

    /// <summary>Physical source column; for <c>json_path</c> fields, the JSON-containing column.</summary>
    public string? SourceColumn { get; init; }

    /// <summary>JSON path used when the field source kind is <c>json_path</c>.</summary>
    public string? JsonPath { get; init; }

    /// <summary>Optional optimized physical column equivalent to the JSON or expression source.</summary>
    public string? MaterializedColumn { get; init; }

    /// <summary>Validated row expression when the field source kind is <c>expression</c>.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>Decimal precision when relevant.</summary>
    public int? NumericPrecision { get; init; }

    /// <summary>Decimal scale when relevant.</summary>
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

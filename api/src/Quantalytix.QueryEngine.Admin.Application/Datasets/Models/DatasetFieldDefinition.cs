namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetFieldDefinition</c>: the writable content of a field (<c>qe_field</c>).</summary>
public abstract record DatasetFieldDefinition
{
    /// <summary>The field key, fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>field_source_kind</c> value.</summary>
    public required string FieldSourceKind { get; init; }

    /// <summary>The physical column; for a JSON path field, the JSON column.</summary>
    public required string? SourceColumn { get; init; }

    /// <summary>The JSON path of a JSON path field.</summary>
    public required string? JsonPath { get; init; }

    /// <summary>The row expression of an expression field.</summary>
    public required string? ExpressionText { get; init; }

    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>The decimal precision; null unless decimal.</summary>
    public required int? NumericPrecision { get; init; }

    /// <summary>The decimal scale; null unless decimal.</summary>
    public required int? NumericScale { get; init; }

    /// <summary>Whether the field may be null.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>The semantic unit, such as <c>USD</c>.</summary>
    public required string? UnitKey { get; init; }

    /// <summary>Whether detail queries may project the field.</summary>
    public required bool IsSelectable { get; init; }

    /// <summary>Whether row filters may reference the field.</summary>
    public required bool IsFilterable { get; init; }

    /// <summary>Whether queries may order by the field.</summary>
    public required bool IsSortable { get; init; }

    /// <summary>Whether the field may be an aggregate grouping on its own.</summary>
    public required bool IsGroupable { get; init; }

    /// <summary>Administrative availability.</summary>
    public required bool IsEnabled { get; init; }
}

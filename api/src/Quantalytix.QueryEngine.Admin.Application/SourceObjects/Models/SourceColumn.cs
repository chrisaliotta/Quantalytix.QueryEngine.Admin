namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A column of a source object with the logical type a field over it gets, as the field pickers offer it.</summary>
public sealed record SourceColumn
{
    /// <summary>The column's name.</summary>
    public required string Name { get; init; }

    /// <summary>The column's StarRocks type as information_schema writes it, such as <c>decimal(20,2)</c>.</summary>
    public required string ColumnType { get; init; }

    /// <summary>The <c>data_type</c> a field over the column gets; null when the type has no logical equivalent, so the column is not offered.</summary>
    public string? DataType { get; init; }

    /// <summary>The decimal precision a field over the column gets; null unless the data type is <c>decimal</c>.</summary>
    public int? NumericPrecision { get; init; }

    /// <summary>The decimal scale a field over the column gets; null unless the data type is <c>decimal</c>.</summary>
    public int? NumericScale { get; init; }

    /// <summary>Whether the column accepts null.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>Whether StarRocks computes the column from an expression, as it does for a materialized JSON path.</summary>
    public required bool IsGenerated { get; init; }
}

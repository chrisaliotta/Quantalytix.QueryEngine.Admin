namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A column of a table or view, as the tenant database's information_schema describes it.</summary>
public sealed record PhysicalColumn
{
    /// <summary>The column's name.</summary>
    public required string Name { get; init; }

    /// <summary>The column's StarRocks type as information_schema writes it, such as <c>decimal(20,2)</c>.</summary>
    public required string ColumnType { get; init; }

    /// <summary>Whether the column accepts null.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>Whether StarRocks computes the column from an expression, as it does for a materialized JSON path.</summary>
    public required bool IsGenerated { get; init; }
}

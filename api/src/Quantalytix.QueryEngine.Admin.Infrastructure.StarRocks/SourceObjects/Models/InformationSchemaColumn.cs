namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>A row of <c>information_schema.columns</c>, limited to the columns the catalog reads.</summary>
internal sealed record InformationSchemaColumn
{
    /// <summary>The column name.</summary>
    public required string ColumnName { get; init; }

    /// <summary>The column's full type, such as <c>decimal(20,2)</c>.</summary>
    public required string ColumnType { get; init; }

    /// <summary><c>YES</c> when the column accepts null, otherwise <c>NO</c>.</summary>
    public required string IsNullable { get; init; }

    /// <summary>The expression of a generated column; null or empty for a stored column.</summary>
    public string? GenerationExpression { get; init; }
}

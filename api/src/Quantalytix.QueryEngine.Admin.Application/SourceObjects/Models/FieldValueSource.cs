namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>Where a column or JSON path field's values are stored: a column of a table or view, or a JSON path inside one.</summary>
public sealed record FieldValueSource
{
    /// <summary>The table or view.</summary>
    public required string ObjectName { get; init; }

    /// <summary>The column; for a JSON path field, the JSON column.</summary>
    public required string ColumnName { get; init; }

    /// <summary>The JSON path, already checked against <c>DatasetPatterns.JsonPath</c>, so it can be inlined; null for a column field.</summary>
    public required string? JsonPath { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>The table a metadata record mirrors. Columns are the record's properties in snake_case.</summary>
public sealed record TableDefinition
{
    /// <summary>The table name, such as <c>qe_dataset</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The database the table lives in.</summary>
    public required TableDatabase Database { get; init; }

    /// <summary>The record properties that form the primary key, in key order.</summary>
    public required IReadOnlyList<string> KeyProperties { get; init; }

    /// <summary>The record properties stored in JSON columns, which are written through <c>PARSE_JSON</c>.</summary>
    public required IReadOnlyList<string> JsonProperties { get; init; }
}

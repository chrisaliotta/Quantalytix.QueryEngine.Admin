namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>A row of <c>information_schema.tables</c>, limited to the columns the catalog reads.</summary>
internal sealed record InformationSchemaTable
{
    /// <summary>The table or view name.</summary>
    public required string TableName { get; init; }

    /// <summary><c>BASE TABLE</c> or <c>VIEW</c>.</summary>
    public required string TableType { get; init; }
}

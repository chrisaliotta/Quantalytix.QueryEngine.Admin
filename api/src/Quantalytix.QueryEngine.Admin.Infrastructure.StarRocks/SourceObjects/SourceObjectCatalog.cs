using Dapper;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>Reads the tables, views and columns of the request tenant's database from its <c>information_schema</c>.</summary>
public sealed class SourceObjectCatalog(IStarRocksConnectionFactory connections) : ISourceObjectCatalog
{
    /// <summary>The tables and views of the connection's database.</summary>
    private const string ObjectsSql = """
        SELECT table_name, table_type
        FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_type IN ('BASE TABLE', 'VIEW')
        """;

    /// <summary>The columns of one table or view of the connection's database, in ordinal order.</summary>
    private const string ColumnsSql = """
        SELECT column_name, column_type, is_nullable, generation_expression
        FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = @ObjectName
        ORDER BY ordinal_position
        """;

    /// <summary>The <c>table_type</c> of a view; every other listed object is a base table.</summary>
    private const string ViewTableType = "VIEW";

    /// <summary>The <c>is_nullable</c> value of a column that accepts null.</summary>
    private const string Nullable = "YES";

    /// <summary>Lists every table and view of the tenant database.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The tables and views, in no particular order.</returns>
    public async Task<IReadOnlyList<SourceObject>> ListObjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        var rows = await connection.QueryAsync<InformationSchemaTable>(new CommandDefinition(ObjectsSql, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new SourceObject { Name = row.TableName, Kind = KindOf(row.TableType) })];
    }

    /// <summary>Lists the columns of one table or view of the tenant database.</summary>
    /// <param name="objectName">The table or view name.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The columns, in ordinal order.</returns>
    public async Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        var command = new CommandDefinition(ColumnsSql, new { ObjectName = objectName }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<InformationSchemaColumn>(command);
        return
        [
            .. rows.Select(row => new PhysicalColumn
            {
                Name = row.ColumnName,
                ColumnType = row.ColumnType,
                IsNullable = row.IsNullable == Nullable,
                IsGenerated = !string.IsNullOrEmpty(row.GenerationExpression)
            })
        ];
    }

    /// <summary>The <c>source_object_kind</c> of an information_schema table type.</summary>
    private static string KindOf(string tableType)
    {
        if (tableType == ViewTableType) return SourceObjectKinds.View;
        return SourceObjectKinds.Table;
    }
}

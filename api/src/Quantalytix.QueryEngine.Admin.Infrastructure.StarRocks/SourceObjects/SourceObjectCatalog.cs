using System.Globalization;
using Dapper;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;

/// <summary>
/// Reads the tables, views, columns and storage statistics of the request tenant's database from its <c>information_schema</c>, and
/// the distinct values of a field from its object.
/// </summary>
/// <param name="connections">Opens the tenant database.</param>
/// <param name="tenant">The request tenant, which keys the cached reads.</param>
/// <param name="statistics">Keeps the statistics briefly.</param>
/// <param name="values">Keeps the distinct values briefly.</param>
public sealed class SourceObjectCatalog(
    IStarRocksConnectionFactory connections,
    ITenantContext tenant,
    SourceReadCache<IReadOnlyDictionary<string, DatasetSourceObjectStats>> statistics,
    SourceReadCache<FieldValues> values) : ISourceObjectCatalog
{
    /// <summary>The tables and views of the connection's database, with the statistics StarRocks keeps.</summary>
    private const string ObjectsSql = """
        SELECT table_name, table_type, table_rows, data_length
        FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_type IN ('BASE TABLE', 'VIEW')
        """;

    /// <summary>The columns of every table and view of the connection's database, by object and ordinal.</summary>
    private const string AllColumnsSql = """
        SELECT table_name, column_name, column_type, is_nullable, generation_expression
        FROM information_schema.columns
        WHERE table_schema = DATABASE()
        ORDER BY table_name, ordinal_position
        """;

    /// <summary>The columns of one table or view of the connection's database, by ordinal.</summary>
    private const string ObjectColumnsSql = """
        SELECT table_name, column_name, column_type, is_nullable, generation_expression
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
        return [.. (await TablesAsync(cancellationToken)).Select(row => new SourceObject { Name = row.TableName, Kind = KindOf(row.TableType) })];
    }

    /// <summary>Lists the columns of one table or view.</summary>
    /// <param name="objectName">The table or view name.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The columns, in ordinal order.</returns>
    public async Task<IReadOnlyList<PhysicalColumn>> ListColumnsAsync(string objectName, CancellationToken cancellationToken)
    {
        var rows = await ColumnsAsync(ObjectColumnsSql, new { ObjectName = objectName }, cancellationToken);
        return [.. rows.Select(Column)];
    }

    /// <summary>Lists the columns of every table and view.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Each object's columns in ordinal order, by object name.</returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<PhysicalColumn>>> ListAllColumnsAsync(CancellationToken cancellationToken)
    {
        var rows = await ColumnsAsync(AllColumnsSql, null, cancellationToken);
        return rows.GroupBy(row => row.TableName, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<PhysicalColumn>)[.. group.Select(Column)], StringComparer.Ordinal);
    }

    /// <summary>Reads the statistics of the tables and views, cached briefly per tenant.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The statistics of each object StarRocks has measured, by name.</returns>
    public Task<IReadOnlyDictionary<string, DatasetSourceObjectStats>> ListStatisticsAsync(CancellationToken cancellationToken)
    {
        return statistics.GetAsync(tenant.TenantId, async measuredAt => (IReadOnlyDictionary<string, DatasetSourceObjectStats>)(await TablesAsync(cancellationToken))
            .Where(row => row.TableRows is not null)
            .ToDictionary(
                row => row.TableName,
                row => new DatasetSourceObjectStats { RowCount = row.TableRows!.Value, SizeBytes = row.DataLength ?? 0, MeasuredAt = measuredAt },
                StringComparer.Ordinal));
    }

    /// <summary>Reads the distinct non-null values of a column or JSON path as text, one more than the limit to tell whether there are more.</summary>
    /// <param name="source">The object, column and JSON path.</param>
    /// <param name="limit">The most values to answer.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The values in ordinal order, and whether they are all there are.</returns>
    public Task<FieldValues> ListDistinctValuesAsync(FieldValueSource source, int limit, CancellationToken cancellationToken)
    {
        var key = string.Join('|', tenant.TenantId, source.ObjectName, source.ColumnName, source.JsonPath, limit.ToString(CultureInfo.InvariantCulture));
        return values.GetAsync(key, async _ =>
        {
            var column = StarRocksDdl.Quote(source.ColumnName);
            var value = source.JsonPath is null ? $"CAST({column} AS VARCHAR)" : $"get_json_string({column}, '{source.JsonPath}')";
            var sql = string.Create(
                CultureInfo.InvariantCulture,
                $"SELECT DISTINCT {value} AS value FROM {StarRocksDdl.Quote(source.ObjectName)} WHERE {value} IS NOT NULL ORDER BY value LIMIT {limit + 1}");
            await using var connection = await connections.OpenTenantAsync(cancellationToken);
            var rows = (await connection.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
            return new FieldValues { Values = [.. rows.Take(limit)], IsComplete = rows.Count <= limit };
        });
    }

    /// <summary>Reads <c>information_schema.tables</c>.</summary>
    private async Task<IEnumerable<InformationSchemaTable>> TablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        return await connection.QueryAsync<InformationSchemaTable>(new CommandDefinition(ObjectsSql, cancellationToken: cancellationToken));
    }

    /// <summary>Reads <c>information_schema.columns</c>.</summary>
    private async Task<IEnumerable<InformationSchemaColumn>> ColumnsAsync(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        return await connection.QueryAsync<InformationSchemaColumn>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>A column as the application describes it.</summary>
    private static PhysicalColumn Column(InformationSchemaColumn row)
    {
        return new PhysicalColumn
        {
            Name = row.ColumnName,
            ColumnType = row.ColumnType,
            IsNullable = row.IsNullable == Nullable,
            IsGenerated = !string.IsNullOrEmpty(row.GenerationExpression)
        };
    }

    /// <summary>The <c>source_object_kind</c> of an information_schema table type.</summary>
    private static string KindOf(string tableType)
    {
        return tableType == ViewTableType ? SourceObjectKinds.View : SourceObjectKinds.Table;
    }
}

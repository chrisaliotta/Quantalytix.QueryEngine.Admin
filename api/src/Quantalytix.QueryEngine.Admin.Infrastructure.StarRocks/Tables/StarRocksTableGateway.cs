using Dapper;
using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>
/// The StarRocks table gateway of one metadata record. Every call opens its own connection to the table's database and
/// runs one statement per call (one per 500 rows for multi-row inserts), because StarRocks rejects UPDATE and DELETE
/// inside transactions.
/// </summary>
/// <typeparam name="TRecord">A record from the Metadata feature.</typeparam>
public sealed class StarRocksTableGateway<TRecord>(IStarRocksConnectionFactory connections) : ITableGateway<TRecord> where TRecord : class
{
    /// <summary>The most rows one INSERT statement writes.</summary>
    private const int InsertChunkSize = 500;

    /// <summary>Lists the rows that match a filter.</summary>
    /// <param name="filter">Null for every row; otherwise an anonymous object of record property values to match.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The matching rows, in no particular order.</returns>
    public async Task<IReadOnlyList<TRecord>> ListAsync(object? filter, CancellationToken cancellationToken)
    {
        if (filter is not null && TableFilter.MatchesNothing(filter)) return [];
        await using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<TRecord>(Command(TableSql<TRecord>.Select(filter), filter, cancellationToken));
        return rows.AsList();
    }

    /// <summary>Reads the one row a primary key identifies.</summary>
    /// <param name="key">An anonymous object holding every key property.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The row.</returns>
    public async Task<TRecord> GetAsync(object key, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<TRecord>(Command(TableSql<TRecord>.Select(key), key, cancellationToken));
    }

    /// <summary>Inserts one row; an insert into a primary key table is an upsert.</summary>
    /// <param name="record">The row to write.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the row is written.</returns>
    public Task InsertAsync(TRecord record, CancellationToken cancellationToken)
    {
        return InsertManyAsync([record], cancellationToken);
    }

    /// <summary>Inserts rows with one multi-row INSERT per 500 rows; does nothing when there are none.</summary>
    /// <param name="records">The rows to write.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every row is written.</returns>
    public async Task InsertManyAsync(IReadOnlyCollection<TRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0) return;
        await using var connection = await OpenAsync(cancellationToken);
        foreach (var chunk in records.Chunk(InsertChunkSize))
        {
            var command = Command(TableSql<TRecord>.Insert(chunk.Length), TableSql<TRecord>.InsertParameters(chunk), cancellationToken);
            await connection.ExecuteAsync(command);
        }
    }

    /// <summary>Sets every non-key column of the row the record's key identifies.</summary>
    /// <param name="record">The row's new values, including its key.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the row is updated.</returns>
    public async Task UpdateAsync(TRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(Command(TableSql<TRecord>.Update, record, cancellationToken));
    }

    /// <summary>Deletes the rows that match a filter.</summary>
    /// <param name="filter">An anonymous object of record property values to match.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    public async Task DeleteAsync(object filter, CancellationToken cancellationToken)
    {
        if (TableFilter.MatchesNothing(filter)) return;
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(Command(TableSql<TRecord>.Delete(filter), filter, cancellationToken));
    }

    /// <summary>Opens the database the record's table lives in.</summary>
    private Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (TableSql<TRecord>.Table.Database == TableDatabase.ControlPlane) return connections.OpenControlPlaneAsync(cancellationToken);
        return connections.OpenTenantAsync(cancellationToken);
    }

    /// <summary>A Dapper command; the timeout comes from the connection's default command timeout.</summary>
    private static CommandDefinition Command(string sql, object? parameters, CancellationToken cancellationToken)
    {
        return new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
    }
}

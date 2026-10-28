using Dapper;
using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

/// <summary>
/// Runs the DDL of managed tables on the request tenant's database. StarRocks answers <c>ALTER TABLE ... ADD COLUMN</c> as soon as it
/// has queued the schema change job, so adding columns polls the table's newest job until it finishes; creating, renaming and
/// dropping a table are synchronous.
/// </summary>
public sealed class StarRocksManagedTables(IStarRocksConnectionFactory connections) : IManagedTables
{
    /// <summary>The newest column schema change job of one table.</summary>
    private const string LatestJobSql = "SHOW ALTER TABLE COLUMN WHERE TableName = @TableName ORDER BY CreateTime DESC LIMIT 1";

    /// <summary>The state of a job whose new columns exist.</summary>
    private const string Finished = "FINISHED";

    /// <summary>The state of a job StarRocks gave up on; its message says why.</summary>
    private const string Cancelled = "CANCELLED";

    /// <summary>How long to wait between two reads of the job's state.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Creates a table with the load timestamp and the given columns.</summary>
    /// <param name="tableName">The new table.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public Task CreateAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken)
    {
        return ExecuteAsync(ManagedTableSql.Create(tableName, columns), cancellationToken);
    }

    /// <summary>Adds columns, then waits until the table's schema change job has finished.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">The new columns.</param>
    /// <param name="cancellationToken">Cancels the statement and the wait.</param>
    /// <returns>A task that completes when the columns exist.</returns>
    public async Task AddColumnsAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(ManagedTableSql.AddColumns(tableName, columns), cancellationToken: cancellationToken));
        var job = await LatestJobAsync(connection, tableName, cancellationToken);
        while (job.State != Finished)
        {
            if (job.State == Cancelled) throw new InvalidOperationException($"StarRocks cancelled the schema change of '{tableName}': {job.Msg}");
            await Task.Delay(PollInterval, cancellationToken);
            job = await LatestJobAsync(connection, tableName, cancellationToken);
        }
    }

    /// <summary>Renames a table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="newName">Its new name.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the table is renamed.</returns>
    public Task RenameAsync(string tableName, string newName, CancellationToken cancellationToken)
    {
        return ExecuteAsync(ManagedTableSql.Rename(tableName, newName), cancellationToken);
    }

    /// <summary>Drops a table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="cancellationToken">Cancels the statement.</param>
    /// <returns>A task that completes when the table is gone.</returns>
    public Task DropAsync(string tableName, CancellationToken cancellationToken)
    {
        return ExecuteAsync(ManagedTableSql.Drop(tableName), cancellationToken);
    }

    /// <summary>Runs one statement on the tenant database.</summary>
    private async Task ExecuteAsync(string statement, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
    }

    /// <summary>Reads the newest column schema change job of a table.</summary>
    private static Task<SchemaChangeJob> LatestJobAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(LatestJobSql, new { TableName = tableName }, cancellationToken: cancellationToken);
        return connection.QuerySingleAsync<SchemaChangeJob>(command);
    }
}

using Dapper;
using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

/// <summary>
/// Runs column changes on tables of the request tenant's database. StarRocks answers <c>ALTER TABLE</c> as soon as it has
/// queued the schema change job, so the executor polls the table's newest job until it finishes.
/// </summary>
public sealed class SchemaChangeExecutor(IStarRocksConnectionFactory connections) : ISchemaChangeExecutor
{
    /// <summary>The newest column schema change job of one table.</summary>
    private const string LatestJobSql = "SHOW ALTER TABLE COLUMN WHERE TableName = @TableName ORDER BY CreateTime DESC LIMIT 1";

    /// <summary>The state of a job whose new columns exist.</summary>
    private const string Finished = "FINISHED";

    /// <summary>The state of a job StarRocks gave up on; its message says why.</summary>
    private const string Cancelled = "CANCELLED";

    /// <summary>How long to wait between two reads of the job's state.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Runs the statement, then waits until the table's schema change job has finished.</summary>
    /// <param name="tableName">The altered table.</param>
    /// <param name="statement">The complete <c>ALTER TABLE</c> statement.</param>
    /// <param name="cancellationToken">Cancels the statement and the wait.</param>
    /// <returns>A task that completes when the new columns exist.</returns>
    public async Task ApplyAsync(string tableName, string statement, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenTenantAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
        var job = await LatestJobAsync(connection, tableName, cancellationToken);
        while (job.State != Finished)
        {
            if (job.State == Cancelled) throw new InvalidOperationException($"StarRocks cancelled the schema change of '{tableName}': {job.Msg}");
            await Task.Delay(PollInterval, cancellationToken);
            job = await LatestJobAsync(connection, tableName, cancellationToken);
        }
    }

    /// <summary>Reads the newest column schema change job of a table.</summary>
    private static Task<SchemaChangeJob> LatestJobAsync(MySqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(LatestJobSql, new { TableName = tableName }, cancellationToken: cancellationToken);
        return connection.QuerySingleAsync<SchemaChangeJob>(command);
    }
}

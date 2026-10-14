namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Runs schema changes on tables of the tenant database. This is the DDL execution seam: StarRocks applies column changes
/// as asynchronous jobs, so the implementation also waits for the job to finish.
/// </summary>
public interface ISchemaChangeExecutor
{
    /// <summary>Runs an <c>ALTER TABLE</c> column change and returns once StarRocks reports the schema change finished.</summary>
    /// <param name="tableName">The altered table, whose schema change job is watched.</param>
    /// <param name="statement">The complete <c>ALTER TABLE</c> statement; DDL binds no parameters, so its text is final.</param>
    /// <param name="cancellationToken">Cancels the statement and the wait.</param>
    /// <returns>A task that completes when the new columns exist.</returns>
    Task ApplyAsync(string tableName, string statement, CancellationToken cancellationToken);
}

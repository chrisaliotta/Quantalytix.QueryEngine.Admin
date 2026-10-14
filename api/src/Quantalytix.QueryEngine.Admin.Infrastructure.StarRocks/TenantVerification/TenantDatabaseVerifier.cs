using Dapper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;

/// <summary>
/// Opens a candidate tenant connection string with the API's connection settings and reads its <c>qe_database_info</c>
/// row. This is the start-of-request check of external input, so any failure, such as a malformed string, a refused
/// connection or a missing table, becomes a failure reason instead of an error; only a cancelled request propagates.
/// </summary>
public sealed class TenantDatabaseVerifier(IStarRocksConnectionFactory connections) : ITenantDatabaseVerifier
{
    /// <summary>Reads the singleton database info row.</summary>
    private const string DatabaseInfoSql = """
        SELECT singleton_id, tenant_key, metadata_schema_version, semantic_revision, active_release_id, updated_at
        FROM qe_database_info
        WHERE singleton_id = 1
        """;

    /// <summary>Opens the connection string and reads the database's <c>qe_database_info</c> row.</summary>
    /// <param name="connectionString">The candidate connection string.</param>
    /// <param name="cancellationToken">Cancels opening and reading.</param>
    /// <returns>The row, or the reason the connection could not be opened or the row read.</returns>
    public async Task<TenantDatabaseProbe> ProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connections.OpenAsync(connectionString, cancellationToken);
            var command = new CommandDefinition(DatabaseInfoSql, cancellationToken: cancellationToken);
            var info = await connection.QuerySingleOrDefaultAsync<DatabaseInfoRecord>(command);
            if (info is null) return new TenantDatabaseProbe { Failure = "The database's qe_database_info table has no row 1." };
            return new TenantDatabaseProbe { DatabaseInfo = info };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new TenantDatabaseProbe { Failure = $"The connection could not be opened or qe_database_info could not be read: {exception.Message}" };
        }
    }
}

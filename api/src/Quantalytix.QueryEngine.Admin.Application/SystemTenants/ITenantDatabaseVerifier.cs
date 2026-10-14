namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Reaches a candidate tenant database through its connection string: the seam between tenant directory validation and
/// the external database a route points at, so validators can be tested without a database.
/// </summary>
public interface ITenantDatabaseVerifier
{
    /// <summary>Opens the connection string with the API's connection settings and reads the database's <c>qe_database_info</c> row.</summary>
    /// <param name="connectionString">The candidate connection string.</param>
    /// <param name="cancellationToken">Cancels opening and reading.</param>
    /// <returns>The row, or the reason the connection could not be opened or the row read.</returns>
    Task<TenantDatabaseProbe> ProbeAsync(string connectionString, CancellationToken cancellationToken);
}

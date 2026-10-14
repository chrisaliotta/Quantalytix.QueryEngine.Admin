using MySqlConnector;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

/// <summary>
/// Opens StarRocks connections for the table gateways and for feature SQL that has no gateway (information_schema
/// introspection, DDL, connection verification). The caller owns and disposes each connection.
/// </summary>
public interface IStarRocksConnectionFactory
{
    /// <summary>Opens the control-plane database (<c>query_control</c>) from <c>ConnectionStrings:ControlPlane</c>.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    Task<MySqlConnection> OpenControlPlaneAsync(CancellationToken cancellationToken);

    /// <summary>Opens the database of the request's tenant, routed through the control plane's tenant directory.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    Task<MySqlConnection> OpenTenantAsync(CancellationToken cancellationToken);

    /// <summary>Opens any StarRocks connection string with the API's connection settings applied.</summary>
    /// <param name="connectionString">A MySQL-protocol connection string, such as a tenant route being verified.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    Task<MySqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken);
}

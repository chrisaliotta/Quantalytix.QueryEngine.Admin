using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;

/// <summary>
/// Opens StarRocks connections with the settings this endpoint needs: no COM_RESET_CONNECTION (StarRocks does not support
/// it), no user variables, DATETIME values read as UTC, and the configured command timeout. Scoped to the request, so the
/// tenant route is read from the control plane at most once per request.
/// </summary>
public sealed class StarRocksConnectionFactory(IConfiguration configuration, IOptions<StarRocksOptions> options, ITenantContext tenant)
    : IStarRocksConnectionFactory
{
    /// <summary>The name of the control-plane connection string.</summary>
    public const string ControlPlaneConnectionName = "ControlPlane";

    /// <summary>Reads a tenant's complete connection string from the tenant directory.</summary>
    private const string TenantRouteSql = "SELECT connection_string FROM qe_tenant_directory WHERE tenant_id = @TenantId";

    /// <summary>The control-plane connection string from configuration.</summary>
    private readonly string _controlPlaneConnectionString = configuration.GetConnectionString(ControlPlaneConnectionName)!;

    /// <summary>The request tenant's connection string, once read.</summary>
    private string? _tenantConnectionString;

    /// <summary>Opens the control-plane database (<c>query_control</c>) from <c>ConnectionStrings:ControlPlane</c>.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    public Task<MySqlConnection> OpenControlPlaneAsync(CancellationToken cancellationToken)
    {
        return OpenAsync(_controlPlaneConnectionString, cancellationToken);
    }

    /// <summary>Opens the database of the request's tenant, routed through the control plane's tenant directory.</summary>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    public async Task<MySqlConnection> OpenTenantAsync(CancellationToken cancellationToken)
    {
        _tenantConnectionString ??= await ReadTenantConnectionStringAsync(cancellationToken);
        return await OpenAsync(_tenantConnectionString, cancellationToken);
    }

    /// <summary>Opens any StarRocks connection string with the API's connection settings applied.</summary>
    /// <param name="connectionString">A MySQL-protocol connection string, such as a tenant route being verified.</param>
    /// <param name="cancellationToken">Cancels opening.</param>
    /// <returns>An open connection.</returns>
    public async Task<MySqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var settings = new MySqlConnectionStringBuilder(connectionString)
        {
            ConnectionReset = false,
            AllowUserVariables = false,
            DateTimeKind = MySqlDateTimeKind.Utc,
            DefaultCommandTimeout = (uint)options.Value.CommandTimeoutSeconds
        };
        var connection = new MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Reads the request tenant's connection string from <c>qe_tenant_directory</c>.</summary>
    private async Task<string> ReadTenantConnectionStringAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenControlPlaneAsync(cancellationToken);
        var command = new CommandDefinition(TenantRouteSql, new { tenant.TenantId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<string>(command);
    }
}

using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;

/// <summary>
/// Masks tenant connection strings with the driver's own parser, so a password given under any of its aliases
/// (<c>Password</c>, <c>pwd</c>) is found. The other options come back in the driver's canonical form.
/// </summary>
public sealed class ConnectionStringMasker : IConnectionStringMasker
{
    /// <summary>The text that replaces a password.</summary>
    public const string MaskedPassword = "*****";

    /// <summary>Replaces the connection string's password with <c>*****</c> and keeps every other part.</summary>
    /// <param name="connectionString">A stored tenant connection string.</param>
    /// <returns>The masked connection string; one without a password is returned without one.</returns>
    public string Mask(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (builder.ContainsKey(nameof(builder.Password))) builder.Password = MaskedPassword;
        return builder.ConnectionString;
    }
}

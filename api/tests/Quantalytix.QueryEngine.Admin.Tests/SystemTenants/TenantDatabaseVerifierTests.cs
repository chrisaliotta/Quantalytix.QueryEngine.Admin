using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SystemTenants;

/// <summary>
/// Confirms that a connection string the driver cannot even parse comes back as a failure reason rather than an error, so
/// the tenant editor shows it on the connection string field instead of failing the request. The string fails while it is
/// parsed, so no connection is attempted.
/// </summary>
public sealed class TenantDatabaseVerifierTests
{
    /// <summary>An option the driver does not know is reported, with the driver's reason.</summary>
    /// <returns>A task that completes when the probe is checked.</returns>
    [Fact]
    public async Task MalformedConnectionString_IsAFailureReason()
    {
        var connections = new StarRocksConnectionFactory(
            new ConfigurationBuilder().Build(),
            Options.Create(new StarRocksOptions()),
            new FixedTenantContext("sandbox"));

        var probe = await new TenantDatabaseVerifier(connections).ProbeAsync(
            "Server=starrocks.invalid;Database=srdb_admin_sandbox;Bogus=1",
            TestContext.Current.CancellationToken);

        Assert.Null(probe.DatabaseInfo);
        Assert.StartsWith("The connection could not be opened or its metadata could not be read: ", probe.Failure);
        Assert.Contains("bogus", probe.Failure, StringComparison.OrdinalIgnoreCase);
    }
}

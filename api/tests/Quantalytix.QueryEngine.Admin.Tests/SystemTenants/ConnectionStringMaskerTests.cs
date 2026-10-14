using MySqlConnector;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SystemTenants;

/// <summary>
/// Confirms that masking replaces a connection string's password, under either of the driver's names for it, and keeps
/// every other part, so the result still describes the route. Tenant connection strings carry credentials, and the
/// tenant directory endpoints must never return a real password.
/// </summary>
public sealed class ConnectionStringMaskerTests
{
    /// <summary>The masker under test.</summary>
    private readonly ConnectionStringMasker _masker = new();

    /// <summary>The password is replaced; server, port, database and user are kept.</summary>
    [Fact]
    public void Password_IsReplacedAndTheRestKept()
    {
        var masked = _masker.Mask("Server=starrocks.example;Port=9030;Database=srdb_admin_sandbox;User ID=admin;Password=s3cret!");

        Assert.DoesNotContain("s3cret", masked);
        var parsed = new MySqlConnectionStringBuilder(masked);
        Assert.Equal(ConnectionStringMasker.MaskedPassword, parsed.Password);
        Assert.Equal("starrocks.example", parsed.Server);
        Assert.Equal(9030u, parsed.Port);
        Assert.Equal("srdb_admin_sandbox", parsed.Database);
        Assert.Equal("admin", parsed.UserID);
    }

    /// <summary>A password given under its <c>pwd</c> alias is masked too.</summary>
    [Fact]
    public void PasswordAlias_IsMasked()
    {
        var masked = _masker.Mask("host=starrocks.example;database=srdb_admin_sandbox;uid=admin;pwd=s3cret!");

        Assert.DoesNotContain("s3cret", masked);
        Assert.Equal(ConnectionStringMasker.MaskedPassword, new MySqlConnectionStringBuilder(masked).Password);
    }

    /// <summary>A connection string without a password gets no placeholder, which would suggest a password exists.</summary>
    [Fact]
    public void MissingPassword_IsNotAdded()
    {
        var masked = _masker.Mask("Server=starrocks.example;Database=srdb_admin_sandbox;User ID=admin");

        Assert.False(new MySqlConnectionStringBuilder(masked).ContainsKey("Password"));
    }
}

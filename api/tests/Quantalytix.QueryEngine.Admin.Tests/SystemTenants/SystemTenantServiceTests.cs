using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SystemTenants;

/// <summary>
/// Confirms the tenant directory's one-tenant reads and edits: an unknown tenant is answered with nothing, which the endpoints turn
/// into the contract's 404 because the directory is outside the tenant tables the shared route check reads; a known tenant is
/// edited, stamped and returned with its password masked.
/// </summary>
public sealed class SystemTenantServiceTests
{
    /// <summary>When the edits happen.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The directory, holding the sandbox tenant.</summary>
    private readonly InMemoryTableGateway<TenantDirectoryRecord> _directory = new(new TenantDirectoryRecord
    {
        TenantId = "sandbox",
        ProviderId = ProviderIds.StarRocks,
        ConnectionString = "Server=starrocks.example;Database=srdb_admin_sandbox;User ID=admin;Password=s3cret!",
        IsEnabled = true,
        MetadataCacheSeconds = 60,
        UpdatedAt = DateTime.UnixEpoch
    });

    /// <summary>Reads and edits of a tenant the directory does not hold answer null and write nothing.</summary>
    /// <returns>A task that completes when the answers are checked.</returns>
    [Fact]
    public async Task UnknownTenant_IsAnsweredWithNothing()
    {
        var service = Service();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Null(await service.GetAsync("missing", cancellationToken));
        Assert.Null(await service.SetEnabledAsync("missing", new TenantEnabledInput { IsEnabled = false }, cancellationToken));
        Assert.Null(await service.UpdateAsync("missing", new TenantUpdateInput { ProviderId = ProviderIds.StarRocks, ConnectionString = null, MetadataCacheSeconds = 5 }, cancellationToken));
        Assert.Equal(["sandbox"], _directory.All.Select(row => row.TenantId));
    }

    /// <summary>Disabling a known tenant writes the flag and the time, and returns the entry with a masked password.</summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task KnownTenant_IsEditedAndMasked()
    {
        var entry = await Service().SetEnabledAsync("sandbox", new TenantEnabledInput { IsEnabled = false }, TestContext.Current.CancellationToken);

        Assert.False(entry!.IsEnabled);
        Assert.DoesNotContain("s3cret", entry.ConnectionString);
        Assert.Equal((false, Now.UtcDateTime), (_directory.All.Single().IsEnabled, _directory.All.Single().UpdatedAt));
    }

    /// <summary>The service over the directory, at <see cref="Now"/>.</summary>
    private SystemTenantService Service()
    {
        return new SystemTenantService(_directory, new ConnectionStringMasker(), new FakeTimeProvider(Now));
    }
}

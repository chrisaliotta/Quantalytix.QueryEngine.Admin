using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the tenant module (blueprint 10).</summary>
public static class TenantFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tenant module, all scoped: the tenant info, catalog and published-member readers, the overview, and the tenant
    /// directory with its connection verification and masking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTenantFeature(this IServiceCollection services)
    {
        services.AddScoped<TenantInfoReader>();
        services.AddScoped<TenantCatalogReader>();
        services.AddScoped<PublishedMembersReader>();
        services.AddScoped<OverviewService>();

        services.AddScoped<SystemTenantService>();
        services.AddScoped<TenantConnectionVerification>();
        services.AddScoped<ITenantDatabaseVerifier, TenantDatabaseVerifier>();
        services.AddScoped<IConnectionStringMasker, ConnectionStringMasker>();
        return services;
    }
}

using Quantalytix.QueryEngine.Admin.WebApi.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registers how a request names the tenant it administers.</summary>
public static class AdminTenancyServiceCollectionExtensions
{
    /// <summary>Registers <see cref="RouteTenantContext"/> as the request's <see cref="ITenantContext"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, RouteTenantContext>();
        return services;
    }
}

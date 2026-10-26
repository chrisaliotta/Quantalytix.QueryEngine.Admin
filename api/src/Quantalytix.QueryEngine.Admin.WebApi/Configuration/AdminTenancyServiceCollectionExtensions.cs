using Quantalytix.QueryEngine.Admin.Api.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registers how a request names the tenant it administers and the rows its route points at.</summary>
public static class AdminTenancyServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RouteTenantContext"/> as the request's <see cref="ITenantContext"/> and
    /// <see cref="RequestRoute"/> as its <see cref="IRequestRoute"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, RouteTenantContext>();
        services.AddScoped<IRequestRoute, RequestRoute>();
        return services;
    }
}

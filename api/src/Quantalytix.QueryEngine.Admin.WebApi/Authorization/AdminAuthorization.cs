using Microsoft.AspNetCore.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Authentication;

namespace Quantalytix.QueryEngine.Admin.WebApi.Authorization;

/// <summary>The API's authorization: every endpoint needs an authenticated caller unless it opts out.</summary>
public static class AdminAuthorization
{
    /// <summary>
    /// Registers a fallback policy that requires an authenticated user, the <see cref="AdminPolicies.TenantAccess"/> policy for
    /// tenant-scoped routes and the <see cref="AdminPolicies.SystemAdmin"/> policy, plus the caller factory they use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<CallerFactory>();
        services.AddSingleton<IAuthorizationHandler, TenantAccessHandler>();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicies.TenantAccess, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantAccessRequirement()))
            .AddPolicy(AdminPolicies.SystemAdmin, policy => policy.RequireRole(AdminAuthentication.SystemAdminRole));
        return services;
    }
}

using Microsoft.AspNetCore.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Authorization;

/// <summary>
/// Admits a SystemAdmin to every tenant, and anyone else to the one tenant their token names. The tenant comes from the
/// <c>tenantId</c> route value of the request being authorized.
/// </summary>
public sealed class TenantAccessHandler(CallerFactory callers) : AuthorizationHandler<TenantAccessRequirement>
{
    /// <summary>Succeeds when the caller may administer the route's tenant; otherwise leaves the requirement unmet.</summary>
    /// <param name="context">The authorization context; its resource is the request's <see cref="HttpContext"/>.</param>
    /// <param name="requirement">The tenant access requirement.</param>
    /// <returns>A completed task.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantAccessRequirement requirement)
    {
        var routeTenantId = (string)((HttpContext)context.Resource!).GetRouteValue(RouteTenantContext.RouteKey)!;
        var caller = callers.Create(context.User);
        if (caller.IsSystemAdmin || string.Equals(caller.TenantId, routeTenantId, StringComparison.Ordinal)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Tenancy;

/// <summary>
/// The request's tenant, taken from the <c>tenantId</c> route value of tenant-scoped routes. The TenantAccess policy has
/// already admitted the caller to it by the time a controller runs.
/// </summary>
public sealed class RouteTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <summary>The route value that names the tenant in every tenant-scoped route.</summary>
    public const string RouteKey = "tenantId";

    /// <summary>The tenant named by the current request's route.</summary>
    public string TenantId
    {
        get
        {
            return (string)httpContextAccessor.HttpContext!.GetRouteValue(RouteKey)!;
        }
    }
}

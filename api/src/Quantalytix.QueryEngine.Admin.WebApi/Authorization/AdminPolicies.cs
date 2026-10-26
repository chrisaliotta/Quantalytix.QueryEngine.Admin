namespace Quantalytix.QueryEngine.Admin.Api.Authorization;

/// <summary>The authorization policy names controllers use.</summary>
public static class AdminPolicies
{
    /// <summary>Admits a caller to a tenant-scoped route when the route's <c>tenantId</c> is the caller's tenant, or the caller is a SystemAdmin.</summary>
    public const string TenantAccess = "TenantAccess";

    /// <summary>Admits only callers with the SystemAdmin role.</summary>
    public const string SystemAdmin = "SystemAdmin";
}

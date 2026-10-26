using Microsoft.AspNetCore.Authorization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Authorization;

/// <summary>Requires the caller to be allowed to administer the tenant named by the route's <c>tenantId</c>.</summary>
public sealed class TenantAccessRequirement : IAuthorizationRequirement;

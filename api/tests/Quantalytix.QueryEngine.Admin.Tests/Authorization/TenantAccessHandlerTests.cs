using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Quantalytix.QueryEngine.Admin.Api.Authentication;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Tenancy;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Authorization;

/// <summary>
/// Confirms who may reach a tenant-scoped route: a caller whose token names the route's tenant, or a SystemAdmin for any
/// tenant; a token for another tenant, or one with two tenant claims, is refused. This policy is the only boundary between
/// one tenant's metadata and another's.
/// </summary>
public sealed class TenantAccessHandlerTests
{
    /// <summary>The handler, reading the default <c>tenant_id</c> claim.</summary>
    private readonly TenantAccessHandler _handler = new(new CallerFactory(Options.Create(new AdminAuthenticationOptions())));

    /// <summary>A token for the route's tenant is admitted.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task OwnTenant_IsAllowed()
    {
        Assert.True(await IsAllowedAsync("sandbox", TenantClaim("sandbox")));
    }

    /// <summary>A token for another tenant is refused.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task OtherTenant_IsDenied()
    {
        Assert.False(await IsAllowedAsync("development", TenantClaim("sandbox")));
    }

    /// <summary>A SystemAdmin is admitted to a tenant other than the one the token names.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task SystemAdmin_IsAllowedAnywhere()
    {
        Assert.True(await IsAllowedAsync("development", TenantClaim("sandbox"), new Claim(AdminAuthentication.RoleClaimType, AdminAuthentication.SystemAdminRole)));
    }

    /// <summary>A token with two tenant claims names no trusted tenant, so even the route matching one of them is refused.</summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task TwoTenantClaims_AreDenied()
    {
        Assert.False(await IsAllowedAsync("sandbox", TenantClaim("sandbox"), TenantClaim("development")));
    }

    /// <summary>Runs the handler for a request to a tenant route, with a caller authenticated like a validated bearer token.</summary>
    private async Task<bool> IsAllowedAsync(string routeTenantId, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, "test-user"), .. claims],
            "Bearer",
            JwtRegisteredClaimNames.Sub,
            AdminAuthentication.RoleClaimType);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues[RouteTenantContext.RouteKey] = routeTenantId;
        var context = new AuthorizationHandlerContext([new TenantAccessRequirement()], new ClaimsPrincipal(identity), httpContext);
        await _handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    /// <summary>A claim naming a tenant, under the default tenant claim type.</summary>
    private static Claim TenantClaim(string tenantId)
    {
        return new Claim(new AdminAuthenticationOptions().TenantClaimType, tenantId);
    }
}

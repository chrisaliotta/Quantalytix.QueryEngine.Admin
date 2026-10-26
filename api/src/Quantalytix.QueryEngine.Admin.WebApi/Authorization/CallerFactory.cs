using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Quantalytix.QueryEngine.Admin.Api.Authentication;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Authorization;

/// <summary>Describes the authenticated caller from the claims of its validated bearer token.</summary>
public sealed class CallerFactory(IOptions<AdminAuthenticationOptions> options)
{
    /// <summary>The claim type that names the caller's tenant.</summary>
    private readonly string _tenantClaimType = options.Value.TenantClaimType;

    /// <summary>Builds the caller of an authenticated principal.</summary>
    /// <param name="principal">The authenticated user.</param>
    /// <returns>The caller's subject, trusted tenant and SystemAdmin role.</returns>
    public Caller Create(ClaimsPrincipal principal)
    {
        return new Caller
        {
            Subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!,
            TenantId = TrustedTenant(principal),
            IsSystemAdmin = principal.IsInRole(AdminAuthentication.SystemAdminRole)
        };
    }

    /// <summary>
    /// The caller's tenant when the token carries exactly one non-blank tenant claim; otherwise null, so an ambiguous token
    /// grants no tenant (the query engine's trusted-tenant rule).
    /// </summary>
    private string? TrustedTenant(ClaimsPrincipal principal)
    {
        var claims = principal.FindAll(_tenantClaimType).ToArray();
        if (claims.Length != 1 || string.IsNullOrWhiteSpace(claims[0].Value)) return null;
        return claims[0].Value;
    }
}

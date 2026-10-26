using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Quantalytix.QueryEngine.Admin.Api.Authentication;

namespace Quantalytix.QueryEngine.Admin.Api.Development;

/// <summary>
/// Issues bearer tokens on a Development host so the web app works without an identity provider. The signing key exists
/// only in process memory, so issued tokens stop validating when the host restarts.
/// </summary>
public sealed class DevelopmentTokenIssuer(IOptions<AdminAuthenticationOptions> authentication, TimeProvider time)
{
    /// <summary>The issuer of development tokens.</summary>
    public const string Issuer = "quantalytix-query-engine-admin-development";

    /// <summary>The audience of development tokens.</summary>
    public const string Audience = "quantalytix-query-engine-admin";

    /// <summary>The subject of development tokens.</summary>
    public const string Subject = "development-user";

    /// <summary>How long a development token stays valid.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>The claim type the token's tenant is written to.</summary>
    private readonly string _tenantClaimType = authentication.Value.TenantClaimType;

    /// <summary>The in-memory HMAC signing key.</summary>
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32)) { KeyId = Issuer };

    /// <summary>
    /// Adds the development issuer, audience and key to the bearer validation parameters. They are appended rather than
    /// replacing the configured ones, so tokens from an identity provider or <c>dotnet user-jwts</c> keep validating.
    /// </summary>
    /// <param name="parameters">The JWT bearer validation parameters.</param>
    public void Trust(TokenValidationParameters parameters)
    {
        parameters.ValidIssuers = [.. parameters.ValidIssuers ?? [], Issuer];
        parameters.ValidAudiences = [.. parameters.ValidAudiences ?? [], Audience];
        parameters.IssuerSigningKeys = [.. parameters.IssuerSigningKeys ?? [], _key];
    }

    /// <summary>Issues a token for a tenant, with the SystemAdmin role when asked.</summary>
    /// <param name="request">The tenant and role to put in the token.</param>
    /// <returns>The signed token and its expiry.</returns>
    public DevelopmentTokenResponse Issue(DevelopmentTokenRequest request)
    {
        var expires = time.GetUtcNow().Add(Lifetime);
        var claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = Subject, [_tenantClaimType] = request.TenantId };
        if (request.SystemAdmin) claims[AdminAuthentication.RoleClaimType] = AdminAuthentication.SystemAdminRole;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        });
        return new DevelopmentTokenResponse { AccessToken = token, TenantId = request.TenantId, SystemAdmin = request.SystemAdmin, ExpiresUtc = expires };
    }
}

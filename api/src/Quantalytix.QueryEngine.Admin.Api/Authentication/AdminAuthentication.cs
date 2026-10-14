using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Quantalytix.QueryEngine.Admin.Api.Authentication;

/// <summary>JWT bearer authentication, configured like the query engine's so the same tokens work against both.</summary>
public static class AdminAuthentication
{
    /// <summary>The claim that carries a validated token's roles, as dotnet user-jwts and the development issuer write it.</summary>
    public const string RoleClaimType = "role";

    /// <summary>The role that admits a caller to every tenant.</summary>
    public const string SystemAdminRole = "SystemAdmin";

    /// <summary>
    /// Registers JWT bearer authentication that requires signed, unexpired tokens from a trusted issuer and audience. The
    /// standard <c>Authentication:Schemes:Bearer</c> configuration supplies issuers, audiences and keys, which also admits
    /// tokens from <c>dotnet user-jwts</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AdminAuthenticationOptions>().BindConfiguration(AdminAuthenticationOptions.SectionName);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            // Claims keep their JWT names, so role and tenant checks read the short claim types rather than mapped URIs.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = RoleClaimType;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
        });
        return services;
    }
}

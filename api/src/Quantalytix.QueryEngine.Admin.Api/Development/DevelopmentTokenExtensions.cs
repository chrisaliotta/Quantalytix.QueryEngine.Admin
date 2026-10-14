using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Quantalytix.QueryEngine.Admin.Api.Development;

/// <summary>Registers development tokens; Program.cs calls it only in the Development environment.</summary>
public static class DevelopmentTokenExtensions
{
    /// <summary>Registers the token issuer and makes the JWT bearer handler trust the tokens it signs.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDevelopmentTokens(this IServiceCollection services)
    {
        services.AddSingleton<DevelopmentTokenIssuer>();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<DevelopmentTokenIssuer>((options, issuer) => issuer.Trust(options.TokenValidationParameters));
        return services;
    }
}

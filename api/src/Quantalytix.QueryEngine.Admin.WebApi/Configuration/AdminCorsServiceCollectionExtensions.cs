using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registers the CORS policy that lets the web app's dev servers call the API.</summary>
public static class AdminCorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a default CORS policy for the origins in <c>Cors:AllowedOrigins</c>, with any header and method. Bearer
    /// tokens travel in the Authorization header, so credentials (cookies) are not allowed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminCors(this IServiceCollection services)
    {
        services.AddOptions<AdminCorsOptions>().BindConfiguration(AdminCorsOptions.SectionName);
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<AdminCorsOptions>>((cors, admin) =>
            cors.AddDefaultPolicy(policy => policy.WithOrigins([.. admin.Value.AllowedOrigins]).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }
}

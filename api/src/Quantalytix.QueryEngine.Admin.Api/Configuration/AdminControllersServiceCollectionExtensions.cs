using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Formatters;
using Quantalytix.QueryEngine.Admin.Api.Development;
using Quantalytix.QueryEngine.Admin.Api.Errors;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registers MVC controllers with the API's validation and JSON conventions.</summary>
public static class AdminControllersServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers with the global <see cref="ValidationFilter"/>, JSON as the only body format, camelCase JSON
    /// (also used for OpenAPI schemas), camelCase FluentValidation paths, and, outside Development, removes the Development
    /// controllers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment, which decides whether Development controllers exist.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminControllers(this IServiceCollection services, IHostEnvironment environment)
    {
        ValidatorOptions.Global.PropertyNameResolver = JsonPropertyPaths.Resolve;
        ValidatorOptions.Global.DisplayNameResolver = JsonPropertyPaths.Resolve;
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
                options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
            })
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
            .ConfigureApplicationPartManager(manager =>
            {
                if (!environment.IsDevelopment()) manager.FeatureProviders.Add(new DevelopmentControllerFeatureProvider());
            });
        return services;
    }

    /// <summary>
    /// The API's JSON conventions, shared by MVC and by the OpenAPI schema generator: camelCase names, numbers only as JSON
    /// numbers (so IDs are plain integers in the schema), nullable annotations enforced (a non-nullable property rejects
    /// null), and enums as camelCase strings.
    /// </summary>
    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.RespectNullableAnnotations = true;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}

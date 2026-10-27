using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Formatters;
using Quantalytix.QueryEngine.Admin.WebApi.Development;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.WebApi.Json;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registers MVC controllers with the API's validation and JSON conventions.</summary>
public static class AdminControllersServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers with the global <see cref="RouteResourceFilter"/> (404 for a route id that names no row) before
    /// the global <see cref="ValidationFilter"/>, binding errors keyed like its errors
    /// (<see cref="BindingErrorResults"/>), bodiless client errors left to the status code pages so they answer a
    /// <see cref="Problem"/> like every other error, JSON as the only body format, camelCase JSON (also used for OpenAPI schemas),
    /// camelCase FluentValidation paths, and, outside Development, removes the Development controllers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment, which decides whether Development controllers exist.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminControllers(this IServiceCollection services, IHostEnvironment environment)
    {
        ValidatorOptions.Global.PropertyNameResolver = JsonPropertyPaths.Resolve;
        ValidatorOptions.Global.DisplayNameResolver = JsonPropertyPaths.Resolve;
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services.AddSingleton<RouteResources>();
        services
            .AddControllers(options =>
            {
                options.Filters.Add<RouteResourceFilter>();
                options.Filters.Add<ValidationFilter>();
                options.OutputFormatters.RemoveType<StringOutputFormatter>();
                options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
                options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single().SupportedMediaTypes.Remove("text/json");
            })
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = BindingErrorResults.Create;
                options.SuppressMapClientErrors = true;
            })
            .ConfigureApplicationPartManager(manager =>
            {
                if (!environment.IsDevelopment()) manager.FeatureProviders.Add(new DevelopmentControllerFeatureProvider());
            });
        return services;
    }

    /// <summary>
    /// The API's JSON conventions, shared by MVC and by the OpenAPI schema generator: camelCase names, ids as strings
    /// (<see cref="StringIds"/>), every other number only as a JSON number, nullable annotations enforced (a non-nullable
    /// property rejects null), enums as camelCase strings, and a union's discriminator (a change-set operation's <c>op</c>)
    /// read wherever it stands in the object and required (<see cref="UnionDiscriminators"/>).
    /// </summary>
    internal static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.AllowOutOfOrderMetadataProperties = true;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.RespectNullableAnnotations = true;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(StringIds.Modify)
            .WithAddedModifier(UnionDiscriminators.Modify);
    }
}

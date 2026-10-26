namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registers the OpenAPI document the build writes to <c>api/openapi/admin-api.json</c> for the web client.</summary>
public static class AdminOpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAPI document with the bearer security scheme, camelCase path and query parameter names, no guessed
    /// request body descriptions, and number or string values for report rows' parameter values. XML comments supply summaries
    /// and descriptions; they are applied before these transformers run.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<CamelCaseParameterNamesTransformer>();
            options.AddOperationTransformer<RequestBodyDescriptionTransformer>();
            options.AddSchemaTransformer<ParameterValuesSchemaTransformer>();
        });
        return services;
    }
}

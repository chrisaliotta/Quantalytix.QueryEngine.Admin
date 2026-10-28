namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registers the OpenAPI document the build writes to <c>api/openapi/admin-api.json</c> for the web client.</summary>
public static class AdminOpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAPI document with the bearer security scheme, camelCase path and query parameter names, string ids in
    /// parameters and schemas, the 404 of every route that names a registered resource, properties omitted when null as
    /// optional, no guessed request body descriptions, number or string scalar values (report row parameter values, band
    /// bounds), and the members of a union (a change set's operations) as objects that require their discriminator. XML comments
    /// supply summaries and descriptions; they are applied before these transformers run.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<CamelCaseParameterNamesTransformer>();
            options.AddOperationTransformer<StringIdParameterTransformer>();
            options.AddOperationTransformer<NotFoundResponseTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesTransformer>();
            options.AddOperationTransformer<RequestBodyDescriptionTransformer>();
            options.AddSchemaTransformer<ScalarValueSchemaTransformer>();
            options.AddSchemaTransformer<UnionMemberSchemaTransformer>();
            options.AddSchemaTransformer<StringIdSchemaTransformer>();
            options.AddSchemaTransformer<OmittedWhenNullSchemaTransformer>();
            options.AddSchemaTransformer<VocabulariesSchemaTransformer>();
        });
        return services;
    }
}

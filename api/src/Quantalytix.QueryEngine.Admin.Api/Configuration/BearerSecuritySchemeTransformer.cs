using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Declares the bearer token scheme in the OpenAPI document and requires it document-wide.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The scheme's name in the document's components.</summary>
    private const string SchemeName = "Bearer";

    /// <summary>Adds the JWT bearer scheme and the document-level security requirement.</summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="context">The transformer context.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.AddComponent<IOpenApiSecurityScheme>(SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A JWT from the identity provider, from dotnet user-jwts, or from POST /development/token on a Development host."
        });
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] }];
        return Task.CompletedTask;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>
/// Describes the parameter values of report rows, a map from parameter key to a JSON number or string, as such. The schema
/// generator writes a map of <see cref="JsonElement"/> as an object without values, which typed clients read as an empty object.
/// </summary>
internal sealed class ParameterValuesSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Gives a parameter value map's schema number or string values.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the schema's type.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(IReadOnlyDictionary<string, JsonElement>))
            schema.AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.String };
        return Task.CompletedTask;
    }
}

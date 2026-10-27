using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes a <see cref="JsonElement"/> as the JSON number or string it always is in this API (a report row's parameter value, a
/// range band bound), and a map of them as such. The schema generator writes a <see cref="JsonElement"/> as an empty schema and a
/// map of them as an object without values, which typed clients read as anything or an empty object.
/// </summary>
internal sealed class ScalarValueSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Gives a scalar value's schema, and a parameter value map's values, the number or string type.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the schema's type.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if ((Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type) == typeof(JsonElement)) schema.Type = JsonSchemaType.Number | JsonSchemaType.String;
        if (context.JsonTypeInfo.Type == typeof(IReadOnlyDictionary<string, JsonElement>))
            schema.AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.String };
        return Task.CompletedTask;
    }
}

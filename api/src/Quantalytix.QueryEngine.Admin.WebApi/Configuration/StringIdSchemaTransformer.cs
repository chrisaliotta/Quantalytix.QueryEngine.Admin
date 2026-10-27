using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.WebApi.Json;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes the id properties <see cref="StringIds"/> converts as the strings they are on the wire. The schema generator cannot
/// see through a custom converter, so it would otherwise describe them as unconstrained values.
/// </summary>
internal sealed class StringIdSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Gives an id property a string schema, nullable for an optional id, or an array of strings for a list of ids.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the property the schema describes.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        switch (context.JsonPropertyInfo?.CustomConverter)
        {
            case StringIdConverter:
                Describe(schema, JsonSchemaType.String);
                break;
            case NullableStringIdConverter:
                Describe(schema, JsonSchemaType.String | JsonSchemaType.Null);
                break;
            case StringIdListConverter:
                Describe(schema, JsonSchemaType.Array);
                schema.Items = new OpenApiSchema { Type = JsonSchemaType.String };
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>Sets a schema's type and drops the integer format the generator may have written.</summary>
    private static void Describe(OpenApiSchema schema, JsonSchemaType type)
    {
        schema.Type = type;
        schema.Format = null;
    }
}

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes a property marked <see cref="JsonIgnoreCondition.WhenWritingNull"/> as optional and not nullable, which is what it
/// is on the wire: it is never <c>null</c>, only absent. The contract states such properties (<c>Problem.detail</c>,
/// <c>Violation.target</c>) that way. Registered after the transformers that set a property's type.
/// </summary>
internal sealed class OmittedWhenNullSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// The metadata key Microsoft.AspNetCore.OpenApi sets on a nullable property whose type becomes a reference; it later wraps
    /// such a property in <c>oneOf: [null, $ref]</c>.
    /// </summary>
    private const string NullablePropertyKey = "x-is-nullable-property";

    /// <summary>Drops the null alternative from the schema of a property that is omitted when null.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the property the schema describes.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var omitted = context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), false)
            .OfType<JsonIgnoreAttribute>()
            .Any(ignore => ignore.Condition == JsonIgnoreCondition.WhenWritingNull) == true;
        if (!omitted) return Task.CompletedTask;
        if (schema.Type is { } type) schema.Type = type & ~JsonSchemaType.Null;
        schema.Metadata?.Remove(NullablePropertyKey);
        return Task.CompletedTask;
    }
}

using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes each member of a <see cref="JsonPolymorphicAttribute"/> union, such as a change set's operations, as the object it is,
/// with its discriminator required. The schema generator writes a member with its properties and discriminator but without a type,
/// and requires the discriminator only on the union.
/// </summary>
internal sealed class UnionMemberSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Types a union member's schema as an object that requires the union's discriminator.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the schema's type.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (DiscriminatorOf(context.JsonTypeInfo.Type) is not { } discriminator) return Task.CompletedTask;
        schema.Type = JsonSchemaType.Object;
        schema.Required ??= new HashSet<string>();
        schema.Required.Add(discriminator);
        return Task.CompletedTask;
    }

    /// <summary>The discriminator of the union a concrete type is a member of, or null when it is no union's member.</summary>
    private static string? DiscriminatorOf(Type type)
    {
        if (type.IsAbstract) return null;
        for (var union = type.BaseType; union is not null; union = union.BaseType)
        {
            if (union.GetCustomAttribute<JsonPolymorphicAttribute>() is { } polymorphic) return polymorphic.TypeDiscriminatorPropertyName;
        }

        return null;
    }
}

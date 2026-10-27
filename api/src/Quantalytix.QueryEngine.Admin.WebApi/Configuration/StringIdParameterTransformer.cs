using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.WebApi.Json;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes path and query parameters that carry ids as strings, like the JSON ids (<see cref="StringIds"/>). A URL value is
/// text either way; the <c>:long</c> route constraints still refuse one that is not a number.
/// </summary>
internal sealed class StringIdParameterTransformer : IOpenApiOperationTransformer
{
    /// <summary>Gives every id parameter of the operation a string schema.</summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="context">The transformer context, holding the action's parameter descriptions.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters is null) return Task.CompletedTask;
        var ids = context.Description.ParameterDescriptions
            .Where(parameter => StringIds.IsId(parameter.Type ?? typeof(object), parameter.Name))
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>().Where(parameter => ids.Contains(parameter.Name!)))
        {
            parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
        }

        return Task.CompletedTask;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Names path and query parameters in camelCase, so the properties of a route or query input record appear as <c>kind</c>
/// or <c>parentId</c>, matching the route template and the JSON naming. It runs after the XML comments are applied, which
/// match those parameters by property name. Binding matches names without regard to case, so requests bind the same way.
/// </summary>
internal sealed class CamelCaseParameterNamesTransformer : IOpenApiOperationTransformer
{
    /// <summary>Renames the operation's path and query parameters to camelCase.</summary>
    /// <param name="operation">The OpenAPI operation, after the XML comments were applied.</param>
    /// <param name="context">The transformer context.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters is null) return Task.CompletedTask;
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>().Where(IsPathOrQuery))
        {
            parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether a parameter travels in the path or the query string.</summary>
    private static bool IsPathOrQuery(OpenApiParameter parameter)
    {
        return parameter.In is ParameterLocation.Path or ParameterLocation.Query;
    }
}

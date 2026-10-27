using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Documents the 404 <see cref="RouteResourceFilter"/> answers on every operation whose route names a registered resource, so
/// no controller declares it by hand.
/// </summary>
/// <param name="resources">The registered route resources.</param>
internal sealed class NotFoundResponseTransformer(RouteResources resources) : IOpenApiOperationTransformer
{
    /// <summary>Adds the 404 problem response when the operation's route names a registered resource.</summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="context">The transformer context, holding the action's route.</param>
    /// <param name="cancellationToken">Cancels the schema lookup.</param>
    /// <returns>A task that completes when the response is added.</returns>
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (resources.In(context.Description.RelativePath!).Count == 0) return;
        var schema = await context.GetOrCreateSchemaAsync(typeof(Problem), null, cancellationToken);
        operation.Responses ??= [];
        operation.Responses["404"] = new OpenApiResponse
        {
            Description = "An id in the route names no row in the tenant.",
            Content = new Dictionary<string, OpenApiMediaType> { [ProblemWriter.ContentType] = new() { Schema = schema } }
        };
    }
}

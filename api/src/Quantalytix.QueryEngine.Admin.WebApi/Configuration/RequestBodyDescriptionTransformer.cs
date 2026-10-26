using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Removes the request body description that the XML-comment generator guesses: it writes every <c>&lt;param&gt;</c> that
/// matches no OpenAPI parameter (such as <c>cancellationToken</c>) to the body, so the last one wins. The body's schema keeps
/// its input type's own summary.
/// </summary>
internal sealed class RequestBodyDescriptionTransformer : IOpenApiOperationTransformer
{
    /// <summary>Clears the request body description of an operation that has a body.</summary>
    /// <param name="operation">The OpenAPI operation, after the XML comments were applied.</param>
    /// <param name="context">The transformer context.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.RequestBody is not null) operation.RequestBody.Description = null;
        return Task.CompletedTask;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Documents the authorization policy on every operation, as the contract's <c>qe/authorization-responses</c> convention does, so
/// no controller declares 401 or 403 by hand: every endpoint that is not anonymous answers 401 without a valid bearer token (the
/// fallback policy), and an endpoint whose <c>[Authorize]</c> names a policy or roles (<c>TenantAccess</c>, <c>SystemAdmin</c>) also
/// answers 403.
/// </summary>
internal sealed class AuthorizationResponsesTransformer : IOpenApiOperationTransformer
{
    /// <summary>Adds the 401 and 403 problem responses the operation's authorization metadata can produce.</summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="context">The transformer context, holding the action's endpoint metadata.</param>
    /// <param name="cancellationToken">Cancels the schema lookup.</param>
    /// <returns>A task that completes when the responses are added.</returns>
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any()) return;
        var schema = await context.GetOrCreateSchemaAsync(typeof(Problem), null, cancellationToken);
        operation.Responses ??= [];
        operation.Responses["401"] = ProblemResponse("No valid bearer token.", schema);
        if (metadata.OfType<IAuthorizeData>().Any(authorize => authorize.Policy is not null || authorize.Roles is not null))
            operation.Responses["403"] = ProblemResponse("No access to the route tenant, or `SystemAdmin` missing on `/api/system/*`.", schema);
    }

    /// <summary>A problem response.</summary>
    private static OpenApiResponse ProblemResponse(string description, IOpenApiSchema schema)
    {
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType> { [ProblemWriter.ContentType] = new() { Schema = schema } }
        };
    }
}

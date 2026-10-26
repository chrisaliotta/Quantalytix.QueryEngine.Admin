using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Versioning;

/// <summary>
/// Put on every action that writes the content or settings of a living kind's version: the action runs while the version is a
/// draft or published, otherwise the request gets 409 "Version is not editable". A successful write to a published version is
/// live at once, so the filter then advances the semantic revision and the query engine reloads. The attribute also documents
/// the 409 response in OpenAPI as a problem document; the content types of the action's other responses are unaffected.
/// </summary>
/// <example><c>[RequireEditable(VersionKinds.DimensionVersion, "dimensionVersionId")]</c> on <c>POST .../dimension-versions/{dimensionVersionId:long}/members</c>.</example>
/// <param name="kind">A living <c>version_kind</c>, such as <c>VersionKinds.DimensionVersion</c>.</param>
/// <param name="routeKey">The route value that holds the version's ID, such as <c>dimensionVersionId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireEditableAttribute(string kind, string routeKey)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json"), IFilterFactory
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>Creates the filter for one request.</summary>
    /// <param name="serviceProvider">The request's services.</param>
    /// <returns>The editability-checking filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new RequireEditableFilter(
            kind,
            routeKey,
            serviceProvider.GetRequiredService<VersionStoreRegistry>(),
            serviceProvider.GetRequiredService<SemanticRevision>(),
            serviceProvider.GetRequiredService<ProblemDetailsFactory>());
    }
}

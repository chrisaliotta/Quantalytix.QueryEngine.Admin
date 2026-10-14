using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Versioning;

/// <summary>
/// Put on every action that writes a version's content: the action runs only while the version is a draft, otherwise the
/// request gets 409 "Version is not a draft". Applies to every kind, semantic releases and assumption sets included. The
/// attribute also documents the 409 response in OpenAPI as a problem document; the content types of the action's other
/// responses are unaffected.
/// </summary>
/// <example><c>[RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]</c> on <c>PUT .../dimension-versions/{dimensionVersionId:long}/members</c>.</example>
/// <param name="kind">The <c>version_kind</c> of the version, such as <c>VersionKinds.DimensionVersion</c>.</param>
/// <param name="routeKey">The route value that holds the version's ID, such as <c>dimensionVersionId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireDraftAttribute(string kind, string routeKey)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json"), IFilterFactory
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>Creates the filter for one request.</summary>
    /// <param name="serviceProvider">The request's services.</param>
    /// <returns>The draft-checking filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new RequireDraftFilter(
            kind,
            routeKey,
            serviceProvider.GetRequiredService<VersionStoreRegistry>(),
            serviceProvider.GetRequiredService<ProblemDetailsFactory>());
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Versioning;

/// <summary>
/// Put on every action that writes a version's content: the action runs only while the version is a draft, otherwise the
/// request gets 409 "Version is not a draft". Applies to every kind, semantic releases and assumption sets included. The
/// attribute also documents the 409 response in OpenAPI.
/// </summary>
/// <example><c>[RequireDraft(VersionKinds.DimensionVersion, "dimensionVersionId")]</c> on <c>PUT .../dimension-versions/{dimensionVersionId:long}/members</c>.</example>
/// <param name="kind">The <c>version_kind</c> of the version, such as <c>VersionKinds.DimensionVersion</c>.</param>
/// <param name="routeKey">The route value that holds the version's ID, such as <c>dimensionVersionId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireDraftAttribute(string kind, string routeKey) : Attribute, IFilterFactory, IApiResponseMetadataProvider
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>The documented body type of the 409 response.</summary>
    public Type Type { get; } = typeof(ProblemDetails);

    /// <summary>The documented status code: 409 Conflict.</summary>
    public int StatusCode { get; } = StatusCodes.Status409Conflict;

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

    /// <summary>Documents the 409 response as a problem document.</summary>
    /// <param name="contentTypes">The response's content types.</param>
    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Add("application/problem+json");
    }
}

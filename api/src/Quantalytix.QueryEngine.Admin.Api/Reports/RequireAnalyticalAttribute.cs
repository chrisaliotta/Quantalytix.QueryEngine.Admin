using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Put on every generic write to a dimension, a bridge or one of their versions: a report owns its report dimension and the
/// bridges of its sources, and its editor keeps them consistent with its rows, so a generic write to them gets 409 "Owned by a
/// report" naming the report. The attribute also documents the 409 response in OpenAPI as a problem document.
/// </summary>
/// <example><c>[RequireAnalytical(AnalyticalTarget.DimensionVersion, "dimensionVersionId")]</c> on <c>POST .../dimension-versions/{dimensionVersionId:long}/members</c>.</example>
/// <param name="target">What the route value names.</param>
/// <param name="routeKey">The route value that holds the object's ID, such as <c>dimensionVersionId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAnalyticalAttribute(AnalyticalTarget target, string routeKey)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json"), IFilterFactory
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>Creates the filter for one request.</summary>
    /// <param name="serviceProvider">The request's services.</param>
    /// <returns>The ownership-checking filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new RequireAnalyticalFilter(
            target,
            routeKey,
            serviceProvider.GetRequiredService<ReportOwnership>(),
            serviceProvider.GetRequiredService<ProblemDetailsFactory>());
    }
}

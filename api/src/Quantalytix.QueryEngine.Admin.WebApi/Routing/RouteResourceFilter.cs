using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Routing;

/// <summary>
/// The shared existence check (blueprint 01: 404 "An id in the route names no row in the tenant"): before every action, each
/// registered resource the route names must exist, outermost first, or the request answers the 404 <see cref="Problem"/>
/// instead of failing deeper with a 500. Registered first among the global filters, so it precedes input validation.
/// </summary>
/// <param name="resources">The registered route resources.</param>
public sealed class RouteResourceFilter(RouteResources resources) : IAsyncActionFilter
{
    /// <summary>Checks every resource of the route, then runs the action only when all exist.</summary>
    /// <param name="context">The action's context, holding its route.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 404 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var route = context.RouteData.Values;
        foreach (var (resource, parameter) in resources.In(context.ActionDescriptor.AttributeRouteInfo!.Template!))
        {
            var exists = await resource.ExistsAsync(context.HttpContext.RequestServices, (string)route[parameter]!, route, context.HttpContext.RequestAborted);
            if (exists) continue;
            context.Result = Problems.OfStatus(StatusCodes.Status404NotFound).ToResult();
            return;
        }

        await next();
    }
}

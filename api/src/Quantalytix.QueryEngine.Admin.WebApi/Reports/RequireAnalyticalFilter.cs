using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Answers 409 instead of running the action when the dimension or bridge a route value names belongs to a report, whose editor
/// alone changes it.
/// </summary>
/// <param name="target">What the route value names.</param>
/// <param name="routeKey">The route value holding the object's ID.</param>
/// <param name="ownership">Finds the owning report.</param>
internal sealed class RequireAnalyticalFilter(
    AnalyticalTarget target,
    string routeKey,
    ReportOwnership ownership) : IAsyncActionFilter
{
    /// <summary>The title of the 409 problem.</summary>
    public const string Title = "Owned by a report";

    /// <summary>Runs the action unless a report owns the object.</summary>
    /// <param name="context">The action's context.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 409 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var id = long.Parse((string)context.RouteData.Values[routeKey]!, CultureInfo.InvariantCulture);
        var cancellationToken = context.HttpContext.RequestAborted;
        var named = Named(context);
        var owner = named switch
        {
            AnalyticalTarget.Dimension => await ownership.OfDimensionAsync(id, cancellationToken),
            AnalyticalTarget.DimensionVersion => await ownership.OfDimensionVersionAsync(id, cancellationToken),
            AnalyticalTarget.Bridge => await ownership.OfBridgeAsync(id, cancellationToken),
            AnalyticalTarget.BridgeVersion => await ownership.OfBridgeVersionAsync(id, cancellationToken),
            _ => null
        };
        if (owner is null)
        {
            await next();
            return;
        }

        var detail = $"{Label(named!.Value)} {id} belongs to report '{owner.ReportKey}'; only the report's editor changes it.";
        context.Result = Problems.Conflict(Title, detail).ToResult();
    }

    /// <summary>What the route names: the target, or for a routed version the kind the route gives when a report can own it.</summary>
    private AnalyticalTarget? Named(ActionExecutingContext context)
    {
        if (target != AnalyticalTarget.RoutedVersion) return target;
        return (string)context.RouteData.Values["kind"]! switch
        {
            VersionKinds.DimensionVersion => AnalyticalTarget.DimensionVersion,
            VersionKinds.BridgeVersion => AnalyticalTarget.BridgeVersion,
            _ => null
        };
    }

    /// <summary>How the detail names the object.</summary>
    private static string Label(AnalyticalTarget named)
    {
        return named switch
        {
            AnalyticalTarget.Dimension => "Dimension",
            AnalyticalTarget.DimensionVersion => "Dimension version",
            AnalyticalTarget.Bridge => "Bridge",
            _ => "Bridge version"
        };
    }
}

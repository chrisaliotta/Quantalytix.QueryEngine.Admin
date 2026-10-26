using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>Advances the semantic revision after a successful write to a model the engine reads: one an enabled report reads.</summary>
/// <param name="routeKey">The route value holding the model's ID.</param>
/// <param name="reports">Reads the reports of the model.</param>
/// <param name="semanticRevision">The revision a live edit advances.</param>
internal sealed class AdvancesModelRevisionFilter(string routeKey, ITableGateway<ReportRecord> reports, SemanticRevision semanticRevision) : IAsyncActionFilter
{
    /// <summary>Runs the action, and advances the revision when it wrote to a model an enabled report reads.</summary>
    /// <param name="context">The action's context.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran and the revision was advanced, if it was.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var model = new { CalculationModelId = long.Parse((string)context.RouteData.Values[routeKey]!, CultureInfo.InvariantCulture) };
        var cancellationToken = context.HttpContext.RequestAborted;
        var executed = await next();
        if (!ExecutedActions.Succeeded(executed)) return;
        if ((await reports.ListAsync(model, cancellationToken)).Any(report => report.IsEnabled))
            await semanticRevision.AdvanceAsync(cancellationToken);
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Advances the semantic revision after a successful write to a report the engine sees: one enabled before or after the write.
/// </summary>
/// <param name="routeKey">The route value holding the report's ID.</param>
/// <param name="reports">Reads whether the report is enabled.</param>
/// <param name="semanticRevision">The revision a live edit advances.</param>
internal sealed class AdvancesReportRevisionFilter(string routeKey, ITableGateway<ReportRecord> reports, SemanticRevision semanticRevision) : IAsyncActionFilter
{
    /// <summary>Runs the action, and advances the revision when it wrote to an enabled report, enabled one, disabled one or deleted one.</summary>
    /// <param name="context">The action's context.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran and the revision was advanced, if it was.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var report = new { ReportId = long.Parse((string)context.RouteData.Values[routeKey]!, CultureInfo.InvariantCulture) };
        var cancellationToken = context.HttpContext.RequestAborted;
        var wasEnabled = (await reports.GetAsync(report, cancellationToken)).IsEnabled;
        var executed = await next();
        if (!ExecutedActions.Succeeded(executed)) return;
        if (wasEnabled || (await reports.ListAsync(report, cancellationToken)).SingleOrDefault() is { IsEnabled: true })
            await semanticRevision.AdvanceAsync(cancellationToken);
    }
}

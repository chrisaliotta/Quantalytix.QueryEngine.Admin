using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Versioning;

/// <summary>
/// Answers 409 instead of running the action when the version named by a route value is neither a draft nor published, and
/// advances the semantic revision after a successful write to a published version.
/// </summary>
/// <param name="kind">The living <c>version_kind</c> of the version.</param>
/// <param name="routeKey">The route value holding the version's ID.</param>
/// <param name="stores">The version stores.</param>
/// <param name="semanticRevision">The revision a live edit advances.</param>
/// <param name="problems">Creates the 409 problem details.</param>
internal sealed class RequireEditableFilter(
    string kind,
    string routeKey,
    VersionStoreRegistry stores,
    SemanticRevision semanticRevision,
    ProblemDetailsFactory problems) : IAsyncActionFilter
{
    /// <summary>The title of the 409 problem.</summary>
    public const string Title = "Version is not editable";

    /// <summary>Loads the version's state, runs the action only when it is a draft or published, and advances the revision after a live edit.</summary>
    /// <param name="context">The action's context.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 409 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var id = long.Parse((string)context.RouteData.Values[routeKey]!, CultureInfo.InvariantCulture);
        var state = await stores.Get(kind).GetAsync(id, context.HttpContext.RequestAborted);
        if (state.Status is not (PublicationStatuses.Draft or PublicationStatuses.Published))
        {
            var detail = $"{kind} {id} is {state.Status}; only a draft or the published version can be edited.";
            var problem = problems.CreateProblemDetails(context.HttpContext, StatusCodes.Status409Conflict, Title, detail: detail);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
            return;
        }

        var executed = await next();
        if (state.Status == PublicationStatuses.Published && ExecutedActions.Succeeded(executed))
            await semanticRevision.AdvanceAsync(context.HttpContext.RequestAborted);
    }
}

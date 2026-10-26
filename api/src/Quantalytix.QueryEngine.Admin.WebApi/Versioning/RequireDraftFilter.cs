using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Versioning;

/// <summary>Answers 409 instead of running the action when the version named by a route value is not a draft.</summary>
/// <param name="kind">The <c>version_kind</c> of the version.</param>
/// <param name="routeKey">The route value holding the version's ID.</param>
/// <param name="stores">The version stores.</param>
/// <param name="problems">Creates the 409 problem details.</param>
internal sealed class RequireDraftFilter(string kind, string routeKey, VersionStoreRegistry stores, ProblemDetailsFactory problems) : IAsyncActionFilter
{
    /// <summary>The title of the 409 problem.</summary>
    public const string Title = "Version is not a draft";

    /// <summary>Loads the version's state and runs the action only when it is a draft.</summary>
    /// <param name="context">The action's context.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 409 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var id = long.Parse((string)context.RouteData.Values[routeKey]!, CultureInfo.InvariantCulture);
        var state = await stores.Get(kind).GetAsync(id, context.HttpContext.RequestAborted);
        if (state.Status != PublicationStatuses.Draft)
        {
            var detail = $"{kind} {id} is {state.Status}; only drafts can be edited.";
            var problem = problems.CreateProblemDetails(context.HttpContext, StatusCodes.Status409Conflict, Title, detail: detail);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
            return;
        }

        await next();
    }
}

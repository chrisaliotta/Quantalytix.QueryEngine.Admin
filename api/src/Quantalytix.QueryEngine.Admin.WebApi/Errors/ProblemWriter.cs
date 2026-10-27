using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace Quantalytix.QueryEngine.Admin.WebApi.Errors;

/// <summary>Writes a <see cref="Problem"/> outside MVC: for unhandled exceptions and for responses that left without a body.</summary>
public static class ProblemWriter
{
    /// <summary>The media type of every problem.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>Answers the unhandled exception <c>UseExceptionHandler</c> caught with the 500 problem.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the problem is written.</returns>
    public static Task WriteInternalAsync(HttpContext context)
    {
        return WriteAsync(context, Problems.Internal(Activity.Current?.Id ?? context.TraceIdentifier));
    }

    /// <summary>Answers a response that left without a body, such as a 401 challenge or an unmatched route, with its problem.</summary>
    /// <param name="context">The status code pages context.</param>
    /// <returns>A task that completes when the problem is written.</returns>
    public static Task WriteStatusAsync(StatusCodeContext context)
    {
        return WriteAsync(context.HttpContext, Problems.OfStatus(context.HttpContext.Response.StatusCode));
    }

    /// <summary>Writes a problem with the API's JSON options.</summary>
    private static Task WriteAsync(HttpContext context, Problem problem)
    {
        context.Response.StatusCode = problem.Status;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: ContentType);
    }
}

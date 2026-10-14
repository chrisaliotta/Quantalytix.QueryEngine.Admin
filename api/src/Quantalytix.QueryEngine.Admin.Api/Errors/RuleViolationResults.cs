using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>Builds the 422 response every controller returns when metadata rules reject a change.</summary>
public static class RuleViolationResults
{
    /// <summary>The title of every rule-violation problem.</summary>
    public const string Title = "The change breaks metadata rules";

    /// <summary>
    /// Creates a 422 <see cref="RuleViolationProblemDetails"/> with the standard problem fields (type, title, status, trace
    /// ID) that MVC's problem details factory fills in.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="detail">What was rejected, such as <c>dimension_version 42 cannot become validated.</c></param>
    /// <param name="violations">The violations to report.</param>
    /// <returns>The 422 result.</returns>
    public static ObjectResult Create(HttpContext httpContext, string detail, IReadOnlyList<RuleViolation> violations)
    {
        var defaults = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateProblemDetails(httpContext, StatusCodes.Status422UnprocessableEntity, Title, detail: detail);
        var problem = new RuleViolationProblemDetails
        {
            Type = defaults.Type,
            Title = defaults.Title,
            Status = defaults.Status,
            Detail = defaults.Detail,
            Instance = defaults.Instance,
            Violations = violations
        };
        foreach (var (key, value) in defaults.Extensions) problem.Extensions[key] = value;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity };
    }
}

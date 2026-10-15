using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>
/// Answers MVC's automatic 400 for a request that failed to bind, keyed the way <see cref="ValidationFilter"/> keys its
/// errors, so a client can show each error on the field it sent. System.Text.Json names the value it could not read by its
/// path from the root, such as <c>$.sortOrder</c> or <c>$.components[0].fieldId</c>: the root prefix is dropped, and the root
/// itself, <c>$</c>, becomes the empty key MVC uses for the whole body. The "The input field is required." entry MVC adds
/// under the body parameter's name only repeats that the body failed to bind, so it is dropped while another entry says why.
/// </summary>
public static class BindingErrorResults
{
    /// <summary>The root of every System.Text.Json path.</summary>
    private const string JsonRoot = "$";

    /// <summary>Creates the 400 answer; MVC calls it as <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/>.</summary>
    /// <param name="context">The action's context, holding the model state the binding errors are in.</param>
    /// <returns>A 400 ValidationProblemDetails result.</returns>
    public static IActionResult Create(ActionContext context)
    {
        var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = problems.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);
        var dropsBodyEntries = problem.Errors.Count > 1;
        problem.Errors = problem.Errors
            .Where(entry => !(dropsBodyEntries && bodyParameters.Contains(entry.Key)))
            .ToDictionary(entry => FieldKey(entry.Key), entry => entry.Value, StringComparer.Ordinal);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>The key <see cref="ValidationFilter"/> would use for a model-state key: a JSON path without its root.</summary>
    private static string FieldKey(string key)
    {
        if (key == JsonRoot) return string.Empty;
        if (key.StartsWith(JsonRoot + ".", StringComparison.Ordinal)) return key[(JsonRoot.Length + 1)..];
        return key;
    }
}

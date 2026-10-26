using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Errors;

/// <summary>
/// Confirms that MVC's automatic 400 for a body that failed to bind is keyed by the JSON path of the value that failed,
/// like the validation filter's errors, so the web forms show the error on that field instead of only in their summary.
/// System.Text.Json reports a value of the wrong type as <c>$.components[0].sortOrder</c>, and MVC adds "The input field is
/// required." under the body parameter, which only repeats that the body failed; that entry stays when it is the only one.
/// The model state below holds the keys MVC produced in the slices' smoke tests.
/// </summary>
public sealed class BindingErrorResultsTests
{
    /// <summary>A value of the wrong type is keyed by its path without the root, and the repeated body entry is dropped.</summary>
    [Fact]
    public void ValueOfTheWrongType_IsKeyedByItsPath()
    {
        var context = Context();
        context.ModelState.AddModelError("$.components[0].sortOrder", "The JSON value could not be converted to System.Int32.");
        context.ModelState.AddModelError("input", "The input field is required.");

        Assert.Equal(["components[0].sortOrder"], Errors(BindingErrorResults.Create(context)).Keys);
    }

    /// <summary>A body of JSON <c>null</c> fails only as the body parameter, so that entry is kept.</summary>
    [Fact]
    public void BodyEntry_IsKeptWhenItIsTheOnlyError()
    {
        var context = Context();
        context.ModelState.AddModelError("input", "The input field is required.");

        Assert.Equal(["input"], Errors(BindingErrorResults.Create(context)).Keys);
    }

    /// <summary>The context of an action whose body binds to its <c>input</c> parameter.</summary>
    private static ActionContext Context()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var action = new ActionDescriptor
        {
            Parameters =
            [
                new ParameterDescriptor
                {
                    Name = "input",
                    ParameterType = typeof(object),
                    BindingInfo = new BindingInfo { BindingSource = BindingSource.Body }
                }
            ]
        };
        return new ActionContext(new DefaultHttpContext { RequestServices = services }, new RouteData(), action);
    }

    /// <summary>The errors of a 400 result.</summary>
    private static IDictionary<string, string[]> Errors(IActionResult result)
    {
        return ((ValidationProblemDetails)((BadRequestObjectResult)result).Value!).Errors;
    }
}

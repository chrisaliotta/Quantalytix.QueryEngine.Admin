using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>
/// Validates every action argument that has a registered FluentValidation validator before the action runs. Failures
/// answer 400 with ValidationProblemDetails keyed by the camelCase JSON paths FluentValidation names, such as
/// <c>components[0].fieldId</c>. The failures are collected apart from the action's model state, whose case-insensitive
/// keys hold bound route and query values under their property names (<c>Kind</c>); MVC has already answered binding errors.
/// </summary>
public sealed class ValidationFilter(ProblemDetailsFactory problems) : IAsyncActionFilter
{
    /// <summary>Validates the arguments, then runs the action only when they are all valid.</summary>
    /// <param name="context">The action's context, holding its bound arguments.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 400 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var failures = new ModelStateDictionary();
        foreach (var argument in context.ActionArguments.Values.OfType<object>())
        {
            if (services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors) failures.AddModelError(failure.PropertyName, failure.ErrorMessage);
        }

        if (!failures.IsValid)
        {
            var problem = problems.CreateValidationProblemDetails(context.HttpContext, failures);
            context.Result = new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            return;
        }

        await next();
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Quantalytix.QueryEngine.Admin.WebApi.Errors;

/// <summary>
/// Validates every action argument that has a registered FluentValidation validator before the action runs. Failures
/// answer the 400 <see cref="Problem"/> keyed by the camelCase JSON paths FluentValidation names, such as
/// <c>components[0].fieldId</c>; MVC has already answered binding errors.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    /// <summary>Validates the arguments, then runs the action only when they are all valid.</summary>
    /// <param name="context">The action's context, holding its bound arguments.</param>
    /// <param name="next">Runs the action.</param>
    /// <returns>A task that completes when the action ran or the 400 result is set.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var argument in context.ActionArguments.Values.OfType<object>())
        {
            if (services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            var errors = failures
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray(), StringComparer.Ordinal);
            context.Result = Problems.Invalid(errors).ToResult();
            return;
        }

        await next();
    }
}

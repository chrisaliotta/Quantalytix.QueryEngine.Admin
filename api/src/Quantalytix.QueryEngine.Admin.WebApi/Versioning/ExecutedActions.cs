using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Quantalytix.QueryEngine.Admin.WebApi.Versioning;

/// <summary>Tells the filters that act after a write whether the write happened.</summary>
internal static class ExecutedActions
{
    /// <summary>A write succeeded when the action threw nothing and answered with a 2xx status, or with no status of its own.</summary>
    /// <param name="executed">The executed action's context.</param>
    /// <returns>True when the write happened.</returns>
    public static bool Succeeded(ActionExecutedContext executed)
    {
        return executed.Exception is null && (executed.Result as IStatusCodeActionResult)?.StatusCode is null or (>= 200 and < 300);
    }
}

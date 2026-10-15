using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Api.Tenancy;

/// <summary>The IDs of the current request's route values; the routes' <c>:long</c> constraints guarantee they parse.</summary>
public sealed class RequestRoute(IHttpContextAccessor httpContextAccessor) : IRequestRoute
{
    /// <summary>The ID a route placeholder holds.</summary>
    /// <param name="placeholder">The placeholder's name, such as <c>datasetId</c>.</param>
    /// <returns>The ID.</returns>
    public long Id(string placeholder)
    {
        var value = (string)httpContextAccessor.HttpContext!.GetRouteValue(placeholder)!;
        return long.Parse(value, CultureInfo.InvariantCulture);
    }
}

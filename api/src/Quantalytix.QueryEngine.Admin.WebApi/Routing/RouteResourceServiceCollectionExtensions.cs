using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.WebApi.Routing;

/// <summary>Registers the route resources <see cref="RouteResourceFilter"/> checks; each module registers its own.</summary>
public static class RouteResourceServiceCollectionExtensions
{
    /// <summary>Registers a resource whose route value is its surrogate id.</summary>
    /// <example><c>services.AddRouteResource&lt;DimensionVersionRecord&gt;("dimension-versions", id =&gt; new { DimensionVersionId = id });</c></example>
    /// <typeparam name="TRecord">The record of the resource's table.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="segment">The literal route segment before the id, such as <c>dimension-versions</c>.</param>
    /// <param name="filterOf">The gateway filter that selects the row with an id.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRouteResource<TRecord>(this IServiceCollection services, string segment, Func<long, object> filterOf)
        where TRecord : class
    {
        return services.AddRouteResource<TRecord>(segment, (value, _) => filterOf(long.Parse(value, CultureInfo.InvariantCulture)));
    }

    /// <summary>Registers a resource whose route value needs other route values to name one row, such as a key within its parent.</summary>
    /// <example><c>services.AddRouteResource&lt;ReportSourceRecord&gt;("sources", (key, route) =&gt; new { ReportId = ..., SourceKey = key });</c></example>
    /// <typeparam name="TRecord">The record of the resource's table.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="segment">The literal route segment before the value.</param>
    /// <param name="filterOf">The gateway filter that selects the row from the value and the request's route values.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRouteResource<TRecord>(
        this IServiceCollection services,
        string segment,
        Func<string, RouteValueDictionary, object> filterOf)
        where TRecord : class
    {
        services.AddSingleton(new RouteResource(segment, async (provider, value, route, cancellationToken) =>
        {
            var rows = await provider.GetRequiredService<ITableGateway<TRecord>>().ListAsync(filterOf(value, route), cancellationToken);
            return rows.Count > 0;
        }));
        return services;
    }
}

using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.CalculationModels;

/// <summary>
/// Put on every action that edits a calculation model or its metrics: the engine reads a model through the enabled reports over it,
/// as it stands, so a successful write to a model an enabled report reads advances the semantic revision and the query engine
/// reloads.
/// </summary>
/// <example><c>[AdvancesModelRevision("modelId")]</c> on <c>PUT .../calculation-models/{modelId:long}/variables/{itemId:long}</c>.</example>
/// <param name="routeKey">The route value that holds the model's ID, such as <c>modelId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdvancesModelRevisionAttribute(string routeKey) : Attribute, IFilterFactory
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>Creates the filter for one request.</summary>
    /// <param name="serviceProvider">The request's services.</param>
    /// <returns>The revision-advancing filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new AdvancesModelRevisionFilter(
            routeKey,
            serviceProvider.GetRequiredService<ITableGateway<ReportRecord>>(),
            serviceProvider.GetRequiredService<SemanticRevision>());
    }
}

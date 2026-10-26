using Microsoft.AspNetCore.Mvc.Filters;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Put on every action that writes a report or its content: the engine reads enabled reports as they stand, so a successful write
/// to one, and enabling or disabling one, advances the semantic revision and the query engine reloads. Writes to a disabled report
/// reach the engine when it is enabled.
/// </summary>
/// <example><c>[AdvancesReportRevision("reportId")]</c> on <c>PUT .../reports/{reportId:long}/rows</c>.</example>
/// <param name="routeKey">The route value that holds the report's ID, such as <c>reportId</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdvancesReportRevisionAttribute(string routeKey) : Attribute, IFilterFactory
{
    /// <summary>False: the filter uses request-scoped services, so MVC creates one per request.</summary>
    public bool IsReusable { get; }

    /// <summary>Creates the filter for one request.</summary>
    /// <param name="serviceProvider">The request's services.</param>
    /// <returns>The revision-advancing filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new AdvancesReportRevisionFilter(
            routeKey,
            serviceProvider.GetRequiredService<ITableGateway<ReportRecord>>(),
            serviceProvider.GetRequiredService<SemanticRevision>());
    }
}

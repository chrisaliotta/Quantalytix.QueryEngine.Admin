using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Api.Reports;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.ReportEditor;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that generic dimension and bridge writes refuse what a report owns: its report dimension and the bridges of its sources,
/// and their versions. Only the report editor keeps them consistent with the report's rows and sources, so a generic write answers
/// 409 naming the report and does not run. Analytical dimensions and bridges, and versions of kinds no report owns, pass.
/// </summary>
public sealed class RequireAnalyticalFilterTests
{
    /// <summary>The report's tables, whose account dimension is analytical.</summary>
    private readonly ReportEditorFixture _fixture = new();

    /// <summary>Writes to report-owned objects are refused, naming the report; writes to analytical ones run.</summary>
    /// <param name="target">What the route names.</param>
    /// <param name="id">The object's ID.</param>
    /// <param name="kind">The route's version kind, for a routed version.</param>
    /// <param name="refused">Whether the write is refused.</param>
    /// <returns>A task that completes when the write is checked.</returns>
    [Theory]
    [InlineData(AnalyticalTarget.Dimension, 500L, null, true)]
    [InlineData(AnalyticalTarget.DimensionVersion, StructureVersionId, null, true)]
    [InlineData(AnalyticalTarget.Bridge, BridgeId, null, true)]
    [InlineData(AnalyticalTarget.BridgeVersion, BridgeVersionId, null, true)]
    [InlineData(AnalyticalTarget.RoutedVersion, StructureVersionId, VersionKinds.DimensionVersion, true)]
    [InlineData(AnalyticalTarget.Dimension, AccountDimensionId, null, false)]
    [InlineData(AnalyticalTarget.DimensionVersion, AccountVersionId, null, false)]
    [InlineData(AnalyticalTarget.RoutedVersion, 41L, VersionKinds.CalculationModelVersion, false)]
    public async Task ReportOwnedObjects_AreRefused(AnalyticalTarget target, long id, string? kind, bool refused)
    {
        var (ran, context) = await RunAsync(target, id, kind);

        Assert.Equal(!refused, ran);
        if (!refused) return;
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.EndsWith($"{id} belongs to report 'balance_sheet'; only the report's editor changes it.", Assert.IsType<ProblemDetails>(result.Value).Detail);
    }

    /// <summary>Runs the guard around a write, reporting whether the write ran.</summary>
    private async Task<(bool Ran, ActionExecutingContext Context)> RunAsync(AnalyticalTarget target, long id, string? kind)
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var routeData = new RouteData();
        routeData.Values["id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (kind is not null) routeData.Values["kind"] = kind;
        var action = new ActionContext(new DefaultHttpContext { RequestServices = services }, routeData, new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var ownership = new ReportOwnership(_fixture.Dimensions, _fixture.DimensionVersions, _fixture.Bridges, _fixture.BridgeVersions, _fixture.Reports);
        var filter = new RequireAnalyticalFilter(target, "id", ownership, services.GetRequiredService<ProblemDetailsFactory>());
        var ran = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()) { Result = new OkResult() });
        });
        return (ran, context);
    }
}

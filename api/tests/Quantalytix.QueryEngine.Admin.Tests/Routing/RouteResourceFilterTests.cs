using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Routing;

/// <summary>
/// Confirms the shared existence check (blueprint 01: 404 "An id in the route names no row in the tenant; a shared existence
/// check runs before every action"): a route id that names no row answers the 404 problem without running the action, instead
/// of the 500 a service's read would raise; the check reads only resources a module registered, keyed by the route segment
/// before the id, so <c>dimension-versions/{versionId}</c> and <c>dataset-versions/{versionId}</c> check different tables.
/// </summary>
public sealed class RouteResourceFilterTests
{
    /// <summary>The template of a child route under a dimension version.</summary>
    private const string MemberTemplate = "api/tenants/{tenantId}/dimension-versions/{versionId:long}/members/{memberId:long}";

    /// <summary>The stored dimension versions.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(new DimensionVersionRecord { DimensionVersionId = 10, DimensionId = 1, VersionNumber = 1, Status = "draft" });

    /// <summary>The stored members.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(new DimensionMemberRecord { DimensionMemberId = 20, DimensionVersionId = 10, MemberKey = "CA", DisplayName = "CA", IsAssignable = true, SortOrder = 1 });

    /// <summary>Every id in the route names a row: the action runs.</summary>
    [Fact]
    public async Task ExistingIds_RunTheAction()
    {
        var (ran, result) = await RunAsync(MemberTemplate, new() { ["versionId"] = "10", ["memberId"] = "20" });

        Assert.True(ran);
        Assert.Null(result);
    }

    /// <summary>A child id that names no row answers 404 and the action does not run.</summary>
    [Fact]
    public async Task MissingChild_AnswersNotFound()
    {
        var (ran, result) = await RunAsync(MemberTemplate, new() { ["versionId"] = "10", ["memberId"] = "99" });

        Assert.False(ran);
        Assert.Equal(ProblemTypes.NotFound, Assert.IsType<Problem>(Assert.IsType<ObjectResult>(result).Value).Type);
    }

    /// <summary>A parent id that names no row answers 404 before the child is read.</summary>
    [Fact]
    public async Task MissingParent_AnswersNotFound()
    {
        var (ran, result) = await RunAsync(MemberTemplate, new() { ["versionId"] = "11", ["memberId"] = "20" });

        Assert.False(ran);
        Assert.Equal(404, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    /// <summary>A segment no module registered is not checked, so routes keep working while their module has not migrated.</summary>
    [Fact]
    public async Task UnregisteredSegment_IsNotChecked()
    {
        var (ran, _) = await RunAsync("api/tenants/{tenantId}/versions/{kind}/{id:long}/status", new() { ["kind"] = "dimension_version", ["id"] = "99" });

        Assert.True(ran);
    }

    /// <summary>Runs the filter over an action with a route template and route values, reporting whether the action ran.</summary>
    private async Task<(bool Ran, IActionResult? Result)> RunAsync(string template, RouteValueDictionary values)
    {
        var services = new ServiceCollection()
            .AddSingleton<ITableGateway<DimensionVersionRecord>>(_versions)
            .AddSingleton<ITableGateway<DimensionMemberRecord>>(_members)
            .AddRouteResource<DimensionVersionRecord>("dimension-versions", id => new { DimensionVersionId = id })
            .AddRouteResource<DimensionMemberRecord>("members", id => new { DimensionMemberId = id })
            .AddSingleton<RouteResources>()
            .BuildServiceProvider();
        var routeData = new RouteData(values);
        var descriptor = new ControllerActionDescriptor { AttributeRouteInfo = new AttributeRouteInfo { Template = template } };
        var action = new ActionContext(new DefaultHttpContext { RequestServices = services }, routeData, descriptor);
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var ran = false;
        await new RouteResourceFilter(services.GetRequiredService<RouteResources>()).OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        return (ran, context.Result);
    }
}

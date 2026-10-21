using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Api.Reports;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms when a report write advances the semantic revision, which the engine keys its metadata cache by. The engine reads enabled
/// reports as they stand, so a successful write to an enabled report, enabling one and disabling one each advance it; a write to a
/// disabled report changes nothing the engine reads, and a refused write changed nothing at all, so both keep it.
/// </summary>
public sealed class AdvancesReportRevisionFilterTests
{
    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 10,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A successful write advances the revision when the report is enabled before or after it.</summary>
    /// <param name="enabledBefore">Whether the report is enabled when the write starts.</param>
    /// <param name="enabledAfter">Whether the write leaves it enabled.</param>
    /// <param name="advanced">Whether the revision advances.</param>
    /// <returns>A task that completes when the write is checked.</returns>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public async Task SuccessfulWrite_AdvancesTheRevisionWhenTheEngineSeesTheReport(bool enabledBefore, bool enabledAfter, bool advanced)
    {
        var reports = new InMemoryTableGateway<ReportRecord>(Report() with { IsEnabled = enabledBefore });

        await WriteAsync(reports, async () =>
        {
            await reports.UpdateAsync(Report() with { IsEnabled = enabledAfter }, TestContext.Current.CancellationToken);
            return new OkResult();
        });

        Assert.Equal(advanced ? StartingRevision + 1 : StartingRevision, Revision());
    }

    /// <summary>A write to an enabled report that the report rules refuse with 422 keeps the revision.</summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task RefusedWrite_KeepsTheRevision()
    {
        var reports = new InMemoryTableGateway<ReportRecord>(Report());

        await WriteAsync(reports, () => Task.FromResult<IActionResult>(new ObjectResult(null) { StatusCode = StatusCodes.Status422UnprocessableEntity }));

        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>Runs the filter around a write to report 60 that the given action makes.</summary>
    private async Task WriteAsync(InMemoryTableGateway<ReportRecord> reports, Func<Task<IActionResult>> write)
    {
        var routeData = new RouteData();
        routeData.Values["reportId"] = ReportId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var action = new ActionContext(new DefaultHttpContext(), routeData, new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var filter = new AdvancesReportRevisionFilter("reportId", reports, new SemanticRevision(_databaseInfo, new FakeTimeProvider()));
        await filter.OnActionExecutionAsync(context, async () => new ActionExecutedContext(action, [], new object()) { Result = await write() });
    }

    /// <summary>The tenant's current semantic revision.</summary>
    private long Revision()
    {
        return Assert.Single(_databaseInfo.All).SemanticRevision;
    }
}

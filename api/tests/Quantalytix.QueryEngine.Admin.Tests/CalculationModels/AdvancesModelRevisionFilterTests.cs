using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Api.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms when a write to a calculation model advances the semantic revision, which the engine keys its metadata cache by. The
/// engine reads a model only through the enabled reports over it, as it stands, so a successful write to a model an enabled report
/// reads advances it; a write to a model only disabled reports read changes nothing the engine reads, and a write the model gate
/// refused changed nothing at all, so both keep it.
/// </summary>
public sealed class AdvancesModelRevisionFilterTests
{
    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 12,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A successful write advances the revision only when the report over the model is enabled.</summary>
    /// <param name="reportEnabled">Whether the report that reads the model is enabled.</param>
    /// <param name="advanced">Whether the revision advances.</param>
    /// <returns>A task that completes when the write is checked.</returns>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SuccessfulWrite_AdvancesTheRevisionWhenAnEnabledReportReadsTheModel(bool reportEnabled, bool advanced)
    {
        await WriteAsync(Report() with { IsEnabled = reportEnabled }, new NoContentResult());

        Assert.Equal(advanced ? StartingRevision + 1 : StartingRevision, Revision());
    }

    /// <summary>A write the model gate refuses with 422 keeps the revision, though an enabled report reads the model.</summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task RefusedWrite_KeepsTheRevision()
    {
        await WriteAsync(Report(), new ObjectResult(null) { StatusCode = StatusCodes.Status422UnprocessableEntity });

        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>Runs the filter around a write to model 40, which the given report reads, that answers the given result.</summary>
    private async Task WriteAsync(ReportRecord report, IActionResult result)
    {
        var routeData = new RouteData();
        routeData.Values["modelId"] = ModelRows.ModelId.ToString(CultureInfo.InvariantCulture);
        var action = new ActionContext(new DefaultHttpContext(), routeData, new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var filter = new AdvancesModelRevisionFilter(
            "modelId", new InMemoryTableGateway<ReportRecord>(report), new SemanticRevision(_databaseInfo, new FakeTimeProvider()));
        await filter.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(action, [], new object()) { Result = result }));
    }

    /// <summary>The tenant's current semantic revision.</summary>
    private long Revision()
    {
        return Assert.Single(_databaseInfo.All).SemanticRevision;
    }
}

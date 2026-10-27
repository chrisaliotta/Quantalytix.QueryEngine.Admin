using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.WebApi.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms the editable guard of living versions: an edit of a draft or the published version runs, and only a successful edit
/// of the published version advances the semantic revision, because queries read the published version and the engine keys its
/// metadata cache by the revision; a failed edit changed nothing, so it keeps the revision. A retired version is history that a
/// historical assumption set may still pin, so the guard answers 409 without running the edit.
/// </summary>
public sealed class RequireEditableFilterTests
{
    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>Dimension versions 1 (retired), 2 (published) and 3 (draft).</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        Version(1, PublicationStatuses.Retired),
        Version(2, PublicationStatuses.Published),
        Version(3, PublicationStatuses.Draft));

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 12,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A successful edit of the published version runs and advances the revision; one of the draft does not advance it.</summary>
    /// <param name="versionId">The edited version.</param>
    /// <param name="advanced">Whether the revision advances.</param>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Theory]
    [InlineData(2L, true)]
    [InlineData(3L, false)]
    public async Task SuccessfulEdit_AdvancesTheRevisionOnlyForThePublishedVersion(long versionId, bool advanced)
    {
        var ran = await EditAsync(versionId, new OkResult());

        Assert.True(ran);
        Assert.Equal(advanced ? StartingRevision + 1 : StartingRevision, Revision());
    }

    /// <summary>An edit of the published version that the validation filter rejects with 400 keeps the revision.</summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task RejectedEdit_KeepsTheRevision()
    {
        await EditAsync(2, new BadRequestResult());

        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>An edit of the retired version answers 409 and does not run.</summary>
    /// <returns>A task that completes when the edit is checked.</returns>
    [Fact]
    public async Task RetiredVersion_IsNotEditable()
    {
        var (ran, context) = await RunAsync(1, new OkResult());

        Assert.False(ran);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>Runs the guard around an edit of a dimension version whose action answers with the given result.</summary>
    private async Task<bool> EditAsync(long versionId, IActionResult result)
    {
        return (await RunAsync(versionId, result)).Ran;
    }

    /// <summary>Runs the guard around an edit of a dimension version, reporting whether the action ran.</summary>
    private async Task<(bool Ran, ActionExecutingContext Context)> RunAsync(long versionId, IActionResult result)
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var routeData = new RouteData();
        routeData.Values["versionId"] = versionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var action = new ActionContext(new DefaultHttpContext { RequestServices = services }, routeData, new ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var filter = new RequireEditableFilter(
            VersionKinds.DimensionVersion,
            "versionId",
            new VersionStoreRegistry([new DimensionVersionStore(_versions)]),
            new SemanticRevision(_databaseInfo, new FakeTimeProvider()));
        var ran = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()) { Result = result });
        });
        return (ran, context);
    }

    /// <summary>The tenant's current semantic revision.</summary>
    private long Revision()
    {
        return Assert.Single(_databaseInfo.All).SemanticRevision;
    }

    /// <summary>A version of dimension 100.</summary>
    private static DimensionVersionRecord Version(long dimensionVersionId, string status)
    {
        return new DimensionVersionRecord { DimensionVersionId = dimensionVersionId, DimensionId = 100, VersionNumber = (int)dimensionVersionId, Status = status };
    }
}

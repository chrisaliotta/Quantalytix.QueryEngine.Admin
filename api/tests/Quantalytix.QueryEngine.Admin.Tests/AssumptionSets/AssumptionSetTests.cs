using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.AssumptionSets;

/// <summary>
/// Confirms the assumption sets that let requests read the default versions or backtest against history (R8–R11, OI-38). The one
/// active set holds the default versions: publishing a living version repoints it while historical sets keep what they captured, and
/// it is never relabelled or deleted. A capture copies the active set's analytical pins and the active release, leaving out what
/// reports own, because reports always run on the active set. Capturing or deleting a set advances the semantic revision, because
/// the engine caches which set keys requests can name by it. The read models tell which pins are still live, which identities moved
/// on since a capture, and whether a historical set can serve each enabled report.
/// </summary>
public sealed class AssumptionSetTests
{
    /// <summary>A capture pins the analytical dimensions only, records the active release and the time, and advances the revision.</summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task Capture_CopiesTheAnalyticalPinsAndTheActiveRelease()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var set = (await fixture.SetCommands().CaptureAsync(new CaptureSetInput { Key = "fy2026_q3", DisplayName = "FY2026 Q3 close", Description = null }, cancellationToken)).Value!;

        Assert.Equal((AssumptionSetStatuses.Historical, (long?)SemanticReleaseId, (DateTime?)Now.UtcDateTime), (set.Status, set.ReleaseId, set.CapturedAt));
        Assert.Equal(
            [new PinnedVersion(VersionKinds.DimensionVersion, RegionDimensionId, 601), new PinnedVersion(VersionKinds.DimensionVersion, AccountDimensionId, 801)],
            await fixture.Pins().ListAsync(set.Id, cancellationToken));
        Assert.Equal(StartingRevision + 1, fixture.CurrentRevision);
        Assert.Equal(RefusalKind.Conflict, (await fixture.SetCommands().CaptureAsync(new CaptureSetInput { Key = "fy2026_q3", DisplayName = "Again", Description = null }, cancellationToken)).Refusal!.Kind);
    }

    /// <summary>The capture preview counts what a capture pins and leaves out, and names the release it records.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task CapturePreview_CountsWhatACaptureLeavesOut()
    {
        var fixture = await CreateAsync();

        var preview = await fixture.SetQueries().CapturePreviewAsync(TestContext.Current.CancellationToken);

        Assert.Equal((2, 0, 0, 1, 1, SemanticReleaseId), (preview.Dimensions, preview.MappingSets, preview.Bridges, preview.LeftOutDimensions, preview.LeftOutBridges, preview.ReleaseId));
    }

    /// <summary>Publishing v2 of the account dimension repoints the active set and leaves the historical set on v1.</summary>
    /// <returns>A task that completes when the pins are checked.</returns>
    [Fact]
    public async Task Publishing_RepointsOnlyTheActiveSet()
    {
        var fixture = await CreateAsync();
        await PublishAccountV2Async(fixture);

        Assert.Equal(802, PinOf(fixture, ActiveSetId));
        Assert.Equal(801, PinOf(fixture, HistoricalSetId));
    }

    /// <summary>
    /// The active set's report-owned pins are flagged and its analytical ones live; once account v1 is frozen behind v2, the historical
    /// set's pin is frozen and counts as changed since the capture.
    /// </summary>
    /// <returns>A task that completes when the pins are checked.</returns>
    [Fact]
    public async Task Pins_CarryTheirStates()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var active = await fixture.SetQueries().GetAsync(ActiveSetId, cancellationToken);
        await PublishAccountV2Async(fixture);
        var list = await fixture.SetQueries().ListAsync(cancellationToken);

        Assert.Equal(
            [(PinKinds.Dimension, AccountDimensionId, PinStates.Live), (PinKinds.Dimension, ReportRows.ReportDimensionId, PinStates.ReportOwned),
             (PinKinds.Dimension, RegionDimensionId, PinStates.Live), (PinKinds.Bridge, AccountBridgeId, PinStates.ReportOwned)],
            active.Pins.Select(pin => (pin.Kind, pin.IdentityId, pin.State)));
        Assert.Null(active.Readiness);
        var historical = Assert.Single(list.Historical);
        Assert.Equal((1, 0, 1), (historical.PinCount, historical.LivePins, historical.ChangedSinceCapture));
        Assert.Equal((4, 2, 0), (list.Active.PinCount, list.Active.LivePins, list.Active.ChangedSinceCapture));
    }

    /// <summary>The historical set cannot serve the balance sheet: it does not pin the region dimension the report reads.</summary>
    /// <returns>A task that completes when the readiness is checked.</returns>
    [Fact]
    public async Task Readiness_NamesTheIdentitiesASetMisses()
    {
        var fixture = await CreateAsync();

        var detail = await fixture.SetQueries().GetAsync(HistoricalSetId, TestContext.Current.CancellationToken);

        var readiness = Assert.Single(detail.Readiness!);
        Assert.False(readiness.Ready);
        var reason = Assert.Single(readiness.Reasons);
        Assert.Equal(("Does not pin dimension 'region'.", PinKinds.Dimension, "600"), (reason.Message, reason.Target.Area, reason.Target.ItemId));
        Assert.Equal(SemanticReleaseId, detail.ReadsReleaseId);
    }

    /// <summary>Comparing the historical set with the active one adds the region pin; report-owned pins and the shared release are left out.</summary>
    /// <returns>A task that completes when the comparison is checked.</returns>
    [Fact]
    public async Task Compare_LeavesOutReportOwnedPins()
    {
        var fixture = await CreateAsync();

        var comparison = (await fixture.SetQueries().CompareAsync(HistoricalSetId, ActiveSetId, TestContext.Current.CancellationToken))!;

        Assert.Equal(RegionDimensionId, Assert.Single(comparison.Added).IdentityId);
        Assert.Empty(comparison.Removed);
        Assert.Equal(1, comparison.Unchanged);
        Assert.Null(comparison.Release);
    }

    /// <summary>The active set is neither relabelled nor deleted; a historical set is relabelled, then deleted with its pins.</summary>
    /// <returns>A task that completes when the commands are checked.</returns>
    [Fact]
    public async Task OnlyHistoricalSets_AreRelabelledAndDeleted()
    {
        var fixture = await CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var commands = fixture.SetCommands();
        var labels = new RelabelSetInput { DisplayName = "Q2 close", Description = "As reported" };

        Assert.Equal(RefusalKind.Conflict, (await commands.RelabelAsync(ActiveSetId, labels, cancellationToken)).Refusal!.Kind);
        Assert.Equal(RefusalKind.Conflict, (await commands.DeleteAsync(ActiveSetId, cancellationToken))!.Kind);
        Assert.Equal("Q2 close", (await commands.RelabelAsync(HistoricalSetId, labels, cancellationToken)).Value!.DisplayName);
        Assert.Equal(StartingRevision, fixture.CurrentRevision);

        Assert.Null(await commands.DeleteAsync(HistoricalSetId, cancellationToken));
        Assert.DoesNotContain(fixture.Sets.All, set => set.AssumptionSetId == HistoricalSetId);
        Assert.DoesNotContain(fixture.DimensionPins.All, pin => pin.AssumptionSetId == HistoricalSetId);
        Assert.Equal(StartingRevision + 1, fixture.CurrentRevision);
    }

    /// <summary>Freezes account v1 behind a published v2 and repoints the active set, as the version workflow does.</summary>
    private static async Task PublishAccountV2Async(ReleaseFixture fixture)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var versions = fixture.Graph.DimensionVersions;
        await versions.UpdateAsync(versions.All.Single(version => version.DimensionVersionId == 801) with { Status = PublicationStatuses.Retired }, cancellationToken);
        await versions.InsertAsync(DimensionRows.Version(802, AccountDimensionId, 2, PublicationStatuses.Published) with { PublishedAt = Now.UtcDateTime }, cancellationToken);
        var pinner = new ActiveSetPinner(VersionKinds.DimensionVersion, new VersionStoreRegistry([new DimensionVersionStore(versions)]), fixture.Pins());
        await pinner.OnPublishedAsync(802, cancellationToken);
    }

    /// <summary>The account dimension version a set pins.</summary>
    private static long PinOf(ReleaseFixture fixture, long setId)
    {
        return fixture.DimensionPins.All.Single(pin => pin.AssumptionSetId == setId && pin.DimensionId == AccountDimensionId).DimensionVersionId;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the analytical bridge lifecycle (blueprint 13, OI-27, B4, B9, B12): a new bridge starts with an empty live version 1,
/// <c>many_to_one</c> / <c>none</c>; endpoints must differ and be analytical and not time; a report bridge is never deleted or frozen
/// here; and a binding blocks the delete.
/// </summary>
public sealed class BridgeCommandsTests
{
    /// <summary>OI-27: create stores the bridge with an empty live version 1, many_to_one / none, pinned by the active set.</summary>
    /// <returns>A task that completes when the command is checked.</returns>
    [Fact]
    public async Task Create_StartsWithAnEmptyManyToOneLiveVersion()
    {
        var fixture = new MappingsFixture();

        var created = await fixture.BridgeCommands().CreateAsync(Input("region_to_branch", MappingsFixture.RegionDimensionId, MappingsFixture.BranchDimensionId), TestContext.Current.CancellationToken);

        Assert.True(created.Succeeded);
        var version = fixture.BridgeVersions.All.Single(row => row.BridgeVersionId == created.Value.VersionId);
        Assert.Equal((1, PublicationStatuses.Published, Cardinalities.ManyToOne, AllocationModes.None), (version.VersionNumber, version.Status, version.Cardinality, version.AllocationMode));
        Assert.Contains(fixture.BridgePins.All, pin => pin.AssumptionSetId == MappingsFixture.ActiveSetId && pin.BridgeVersionId == version.BridgeVersionId);
    }

    /// <summary>B4: equal endpoints, a time endpoint and a report dimension are each a conflict.</summary>
    /// <returns>A task that completes when the commands are checked.</returns>
    [Fact]
    public async Task Create_RefusesEndpointsB4Forbids()
    {
        var commands = new MappingsFixture().BridgeCommands();
        var cancellationToken = TestContext.Current.CancellationToken;

        var same = await commands.CreateAsync(Input("same", MappingsFixture.BranchDimensionId, MappingsFixture.BranchDimensionId), cancellationToken);
        var time = await commands.CreateAsync(Input("time", MappingsFixture.TimeDimensionId, MappingsFixture.BranchDimensionId), cancellationToken);
        var report = await commands.CreateAsync(Input("report", MappingsFixture.RegionDimensionId, ReportRows.ReportDimensionId), cancellationToken);

        Assert.All(new[] { same, time, report }, outcome => Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind));
    }

    /// <summary>B9: a report bridge's delete check names its owner, and its delete and freeze are refused as owned by a report.</summary>
    /// <returns>A task that completes when the commands are checked.</returns>
    [Fact]
    public async Task ReportBridge_IsNeitherDeletedNorFrozenHere()
    {
        var fixture = new MappingsFixture();
        var cancellationToken = TestContext.Current.CancellationToken;

        var check = await fixture.BridgeQueries().BridgeDeleteCheckAsync(MappingsFixture.ReportBridgeId, cancellationToken);
        var deleted = await fixture.BridgeCommands().DeleteAsync(MappingsFixture.ReportBridgeId, cancellationToken);
        var frozen = await fixture.BridgeCommands().FreezeAsync(MappingsFixture.ReportBridgeVersionId, null, cancellationToken);

        Assert.Equal([DeleteBlockerCodes.OwnedByReport], check.Reasons.Select(reason => reason.Code));
        Assert.Equal(BridgeRefusals.OwnedByReportTitle, deleted!.Title);
        Assert.Equal(BridgeRefusals.OwnedByReportTitle, frozen.Refusal!.Title);
        Assert.Contains(fixture.Bridges.All, bridge => bridge.BridgeId == MappingsFixture.ReportBridgeId);
    }

    /// <summary>B12: a binding blocks deleting an analytical bridge; without it the bridge goes with its versions, mappings and pins.</summary>
    /// <returns>A task that completes when the deletes are checked.</returns>
    [Fact]
    public async Task Delete_IsBlockedByBindings_ThenRemovesEverything()
    {
        var fixture = new MappingsFixture();
        await fixture.BindAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var blocked = await fixture.BridgeCommands().DeleteAsync(MappingsFixture.BridgeId, cancellationToken);
        await fixture.Bindings.DeleteAsync(new { BridgeId = MappingsFixture.BridgeId }, cancellationToken);
        var deleted = await fixture.BridgeCommands().DeleteAsync(MappingsFixture.BridgeId, cancellationToken);

        Assert.Equal([DeleteBlockerCodes.ReferencedByDatasetBinding], blocked!.Violations.Select(reason => reason.Code));
        Assert.Null(deleted);
        Assert.DoesNotContain(fixture.Bridges.All, bridge => bridge.BridgeId == MappingsFixture.BridgeId);
        Assert.DoesNotContain(fixture.BridgeVersions.All, version => version.BridgeId == MappingsFixture.BridgeId);
        Assert.DoesNotContain(fixture.BridgeMappings.All, mapping => mapping.BridgeVersionId == MappingsFixture.LiveBridgeVersionId);
        Assert.DoesNotContain(fixture.BridgePins.All, pin => pin.BridgeId == MappingsFixture.BridgeId);
    }

    /// <summary>A new analytical bridge between two dimensions.</summary>
    private static CreateBridgeInput Input(string key, long sourceDimensionId, long targetDimensionId)
    {
        return new CreateBridgeInput { Key = key, DisplayName = key, SourceDimensionId = sourceDimensionId, TargetDimensionId = targetDimensionId, IsEnabled = true };
    }
}

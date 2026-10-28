using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the mapping set lifecycle (blueprint 13, OI-14, OI-27, B1, B12): a new mapping set starts with an empty live version 1
/// pinned by the active set; only analytical categorical or hierarchical dimensions take mapping sets, and keys are unique; a binding or
/// a historical pin blocks the delete; only an unpinned frozen version deletes; and freeze continues in a live copy.
/// </summary>
public sealed class MappingCommandsTests
{
    /// <summary>OI-27: create stores the set with an empty live version 1, pinned by the active set, and advances the revision.</summary>
    /// <returns>A task that completes when the command is checked.</returns>
    [Fact]
    public async Task Create_StartsWithAnEmptyLiveVersion()
    {
        var fixture = new MappingsFixture();

        var created = await fixture.MappingCommands().CreateAsync(Input("bank.region_codes", MappingsFixture.RegionDimensionId), TestContext.Current.CancellationToken);

        Assert.True(created.Succeeded);
        var version = fixture.MappingVersions.All.Single(row => row.MappingVersionId == created.Value.VersionId);
        Assert.Equal((1, PublicationStatuses.Published), (version.VersionNumber, version.Status));
        Assert.Contains(fixture.MappingPins.All, pin => pin.AssumptionSetId == MappingsFixture.ActiveSetId && pin.MappingVersionId == version.MappingVersionId);
        Assert.True(fixture.Revision > MappingsFixture.StartingRevision);
    }

    /// <summary>B1 and unique keys: a report or time dimension, or a key another set has in any case, is a conflict; a missing dimension is invalid input.</summary>
    /// <returns>A task that completes when the commands are checked.</returns>
    [Fact]
    public async Task Create_RefusesOtherDimensionsAndTakenKeys()
    {
        var commands = new MappingsFixture().MappingCommands();
        var cancellationToken = TestContext.Current.CancellationToken;

        var report = await commands.CreateAsync(Input("rows", ReportRows.ReportDimensionId), cancellationToken);
        var time = await commands.CreateAsync(Input("dates", MappingsFixture.TimeDimensionId), cancellationToken);
        var taken = await commands.CreateAsync(Input("ZZ_MAP_CODES_300", MappingsFixture.RegionDimensionId), cancellationToken);
        var missing = await commands.CreateAsync(Input("nowhere", 12345), cancellationToken);

        Assert.Equal(
            [RefusalKind.Conflict, RefusalKind.Conflict, RefusalKind.Conflict, RefusalKind.Invalid],
            new[] { report, time, taken, missing }.Select(outcome => outcome.Refusal!.Kind));
        Assert.Contains("dimensionId", missing.Refusal!.Errors.Keys);
    }

    /// <summary>B12: a binding and a historical pin block the delete, each with its code; without them the set goes with every version and pin.</summary>
    /// <returns>A task that completes when the deletes are checked.</returns>
    [Fact]
    public async Task Delete_IsBlockedByBindingsAndHistoricalPins_ThenRemovesEverything()
    {
        var fixture = new MappingsFixture();
        await fixture.BindAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var blocked = await fixture.MappingCommands().DeleteAsync(MappingsFixture.MappingSetId, cancellationToken);
        await fixture.Bindings.DeleteAsync(new { MappingSetId = MappingsFixture.MappingSetId }, cancellationToken);
        await fixture.MappingPins.DeleteAsync(new { AssumptionSetId = MappingsFixture.HistoricalSetId }, cancellationToken);
        var deleted = await fixture.MappingCommands().DeleteAsync(MappingsFixture.MappingSetId, cancellationToken);

        Assert.Equal(
            [DeleteBlockerCodes.ReferencedByDatasetBinding, DeleteBlockerCodes.ReferencedByAssumptionSet],
            blocked!.Violations.Select(reason => reason.Code));
        Assert.Null(deleted);
        Assert.Empty(fixture.MappingSets.All);
        Assert.Empty(fixture.MappingVersions.All);
        Assert.Empty(fixture.Entries.All);
        Assert.Empty(fixture.MappingPins.All);
    }

    /// <summary>Only frozen versions delete: the live one is blocked with <c>live_version</c>, a pinned frozen one by its historical set.</summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task VersionDeleteCheck_BlocksTheLiveAndPinnedVersions()
    {
        var queries = new MappingsFixture().MappingQueries();

        var live = await queries.VersionDeleteCheckAsync(MappingsFixture.LiveMappingVersionId, TestContext.Current.CancellationToken);
        var pinned = await queries.VersionDeleteCheckAsync(MappingsFixture.FrozenMappingVersionId, TestContext.Current.CancellationToken);

        Assert.Equal([DeleteBlockerCodes.LiveVersion], live.Reasons.Select(reason => reason.Code));
        Assert.Equal([DeleteBlockerCodes.ReferencedByAssumptionSet], pinned.Reasons.Select(reason => reason.Code));
    }

    /// <summary>OI-14: freeze retires the live version with its note and continues in a live copy of its entries, which the active set pins.</summary>
    /// <returns>A task that completes when the freeze is checked.</returns>
    [Fact]
    public async Task Freeze_ContinuesInALiveCopy()
    {
        var fixture = new MappingsFixture();

        var frozen = await fixture.MappingCommands().FreezeAsync(MappingsFixture.LiveMappingVersionId, " FY26 close ", TestContext.Current.CancellationToken);

        Assert.True(frozen.Succeeded);
        Assert.Equal((3, PublicationStatuses.Published, 1), (frozen.Value.Live.VersionNumber, frozen.Value.Live.Status, frozen.Value.Live.EntryCount));
        var old = fixture.MappingVersions.All.Single(row => row.MappingVersionId == MappingsFixture.LiveMappingVersionId);
        Assert.Equal((PublicationStatuses.Retired, "FY26 close"), (old.Status, old.FreezeNote));
        Assert.Contains(fixture.MappingPins.All, pin => pin.AssumptionSetId == MappingsFixture.ActiveSetId && pin.MappingVersionId == frozen.Value.Live.Id);
    }

    /// <summary>A new mapping set onto a dimension.</summary>
    private static CreateMappingSetInput Input(string key, long dimensionId)
    {
        return new CreateMappingSetInput { Key = key, DisplayName = key, SourceNamespace = "core_banking", DimensionId = dimensionId };
    }
}

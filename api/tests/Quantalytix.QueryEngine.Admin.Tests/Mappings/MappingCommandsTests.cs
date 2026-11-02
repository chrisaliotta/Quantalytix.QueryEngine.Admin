using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the mapping set lifecycle (blueprint 13, OI-27, B1, B12): a new mapping set starts with an empty live version; only
/// analytical categorical or hierarchical dimensions take mapping sets, and keys are unique; and a binding or a frozen snapshot
/// that holds the set blocks the delete, which otherwise removes the set with every version.
/// </summary>
public sealed class MappingCommandsTests
{
    /// <summary>OI-27: create stores the set with an empty version in the live snapshot and advances the revision.</summary>
    /// <returns>A task that completes when the command is checked.</returns>
    [Fact]
    public async Task Create_StartsWithAnEmptyLiveVersion()
    {
        var fixture = new MappingsFixture();

        var created = await fixture.MappingCommands().CreateAsync(Input("bank.region_codes", MappingsFixture.RegionDimensionId), TestContext.Current.CancellationToken);

        Assert.True(created.Succeeded);
        var version = fixture.MappingVersions.All.Single(row => row.MappingVersionId == created.Value.VersionId);
        Assert.Equal((created.Value.MappingSetId, SnapshotRows.LiveId), (version.MappingSetId, version.SnapshotId));
        Assert.DoesNotContain(fixture.Entries.All, entry => entry.MappingVersionId == version.MappingVersionId);
        Assert.Equal(MappingsFixture.StartingRevision + 1, fixture.Revision);
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

    /// <summary>
    /// B12: a binding and the frozen snapshot that holds the set block the delete, each with its code; without them the set goes with
    /// every version and entry, and the revision advances.
    /// </summary>
    /// <returns>A task that completes when the deletes are checked.</returns>
    [Fact]
    public async Task Delete_IsBlockedByBindingsAndFrozenSnapshots_ThenRemovesEverything()
    {
        var fixture = new MappingsFixture();
        await fixture.BindAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var blocked = await fixture.MappingCommands().DeleteAsync(MappingsFixture.MappingSetId, cancellationToken);
        await fixture.Bindings.DeleteAsync(new { MappingSetId = MappingsFixture.MappingSetId }, cancellationToken);
        await fixture.MappingRemover().RemoveVersionAsync(MappingsFixture.FrozenMappingVersionId, cancellationToken);
        var deleted = await fixture.MappingCommands().DeleteAsync(MappingsFixture.MappingSetId, cancellationToken);

        Assert.Equal(
            [DeleteBlockerCodes.ReferencedByDatasetBinding, DeleteBlockerCodes.ReferencedBySnapshot],
            blocked!.Violations.Select(reason => reason.Code));
        Assert.Null(deleted);
        Assert.Empty(fixture.MappingSets.All);
        Assert.Empty(fixture.MappingVersions.All);
        Assert.Empty(fixture.Entries.All);
        Assert.Equal(MappingsFixture.StartingRevision + 1, fixture.Revision);
    }

    /// <summary>A new mapping set onto a dimension.</summary>
    private static CreateMappingSetInput Input(string key, long dimensionId)
    {
        return new CreateMappingSetInput { Key = key, DisplayName = key, SourceNamespace = "core_banking", DimensionId = dimensionId };
    }
}

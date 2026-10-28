using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the mapping set change set (OI-01, B11): edits apply to the identity and the live version's entries in memory; a preview
/// flags the entries that break a rule so the editor can show them; a commit writes only a result without findings, maps temporary ids
/// to stored ids and advances the semantic revision once; and an operation on a frozen version or a gone entry is a conflict.
/// </summary>
public sealed class MappingSetChangeSetTests
{
    /// <summary>The live version, as operations name it.</summary>
    private static readonly string Live = MappingsFixture.LiveMappingVersionId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A preview shows a new entry under its temporary id and the coverage it leaves; a commit stores it under a real id.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task NewEntry_IsPreviewedByItsTemporaryId_AndCommittedUnderAStoredId()
    {
        var fixture = new MappingsFixture();
        var changes = fixture.MappingChanges();
        var request = Request(
            new MappingSetUpdateOperation { Patch = new MappingSetPatch { DisplayName = "Branch codes" } },
            new MappingEntrySaveOperation { VersionId = Live, Input = new MappingEntryInput { Id = "tmp:ny", SourceKeyValue = "36", MemberKey = "NY" } });

        var preview = await changes.PreviewAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);
        var commit = await changes.CommitAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded);
        Assert.Contains(preview.Value.State.Content.Entries, entry => entry.Id == "tmp:ny");
        Assert.Empty(preview.Value.State.Content.Coverage.UncoveredMemberKeys);
        Assert.Equal(2, preview.Value.State.Set.PublishedEntryCount);
        Assert.True(commit.Succeeded);
        var stored = commit.Value.IdMap["tmp:ny"];
        Assert.Contains(fixture.Entries.All, entry => entry.DimensionMappingId == stored && entry.MemberKey == "NY");
        Assert.Equal("Branch codes", fixture.MappingSets.All.Single().DisplayName);
        Assert.Equal(MappingsFixture.StartingRevision + 1, fixture.Revision);
    }

    /// <summary>B11: an entry the rules reject is flagged in the preview's state and findings, and refuses the commit with nothing written.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task BrokenEntry_IsFlaggedInThePreview_AndRefusesTheCommit()
    {
        var fixture = new MappingsFixture();
        var changes = fixture.MappingChanges();
        var request = Request(new MappingEntrySaveOperation { VersionId = Live, Input = new MappingEntryInput { Id = "tmp:us", SourceKeyValue = "00", MemberKey = "US" } });

        var preview = await changes.PreviewAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);
        var commit = await changes.CommitAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);

        var finding = Assert.Single(preview.Value!.Violations);
        Assert.Equal((EntryMemberAssignableRule.Code, "tmp:us"), (finding.Code, finding.Target!.ItemId));
        Assert.Equal(preview.Value.Violations, preview.Value.State.Content.Violations);
        Assert.Equal(RefusalKind.RulesViolated, commit.Refusal!.Kind);
        Assert.Equal(2, fixture.Entries.All.Count);
        Assert.Equal(MappingsFixture.StartingRevision, fixture.Revision);
    }

    /// <summary>Replacing the entries keeps the stored ids it names, adds the temporary ones and removes the rest.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task ReplaceEntries_KeepsNamedIds_AndRemovesTheRest()
    {
        var fixture = new MappingsFixture();
        var request = Request(new MappingEntriesReplaceOperation
        {
            VersionId = Live,
            Entries =
            [
                new MappingEntryInput { Id = "400", SourceKeyValue = "06", MemberKey = "NY" },
                new MappingEntryInput { Id = "tmp:ca", SourceKeyValue = "48", MemberKey = "CA" }
            ]
        });

        var commit = await fixture.MappingChanges().CommitAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);

        Assert.True(commit.Succeeded);
        Assert.Equal(
            [("06", "NY", "400"), ("48", "CA", commit.Value.IdMap["tmp:ca"].ToString(System.Globalization.CultureInfo.InvariantCulture))],
            commit.Value.State.Content.Entries.Select(entry => (entry.SourceKeyValue, entry.MemberKey, entry.Id)));
    }

    /// <summary>Frozen versions accept no edits: an operation naming one is a conflict.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task OperationOnAFrozenVersion_IsAConflict()
    {
        var request = Request(new MappingEntryDeleteOperation { VersionId = MappingsFixture.FrozenMappingVersionId.ToString(System.Globalization.CultureInfo.InvariantCulture), EntryId = "401" });

        var preview = await new MappingsFixture().MappingChanges().PreviewAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);

        Assert.Equal((RefusalKind.Conflict, "Version is not live"), (preview.Refusal!.Kind, preview.Refusal.Title));
    }

    /// <summary>A stored entry another author deleted is a conflict, as a commit replayed onto newer rows is (OI-02).</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task SavingAGoneEntry_IsAConflict()
    {
        var request = Request(new MappingEntrySaveOperation { VersionId = Live, Input = new MappingEntryInput { Id = "999", SourceKeyValue = "06", MemberKey = "CA" } });

        var preview = await new MappingsFixture().MappingChanges().PreviewAsync(MappingsFixture.MappingSetId, request, TestContext.Current.CancellationToken);

        Assert.Equal((RefusalKind.Conflict, "Entry not found"), (preview.Refusal!.Kind, preview.Refusal.Title));
    }

    /// <summary>A change set of operations.</summary>
    private static ChangeSetRequest<MappingOperation> Request(params MappingOperation[] operations)
    {
        return new ChangeSetRequest<MappingOperation> { Operations = operations };
    }
}

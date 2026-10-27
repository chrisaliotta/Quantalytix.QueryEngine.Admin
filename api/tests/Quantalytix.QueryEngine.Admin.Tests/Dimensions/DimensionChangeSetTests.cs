using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms the dimension change set the dimension editor saves through (blueprint 12, OI-01): edits apply to the live version
/// in memory, a preview names what it creates by the client's temporary ids so later edits can reference them, a commit writes
/// the rows with the closure rebuilt and maps the temporary ids, and the refusals the editor shows come back with their status:
/// 400 for input, 409 for a frozen version, a locked profile or a blocked delete, and publication findings that track the pending
/// edits rather than the stored version.
/// </summary>
public sealed class DimensionChangeSetTests
{
    /// <summary>The live version's id as operations carry it.</summary>
    private const string Live = "101";

    /// <summary>The tables and pipeline.</summary>
    private readonly DimensionFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A preview of a parent and a child created under it by temporary id shows both by their temporary ids, so the editor's next
    /// preview can name them again, and writes nothing.
    /// </summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_PresentsCreatedItemsByTemporaryId()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(DimensionFixture.DimensionId, Request(SaveMember("tmp:north", "NORTH"), SaveMember("tmp:br01", "BR01", "tmp:north")), Token);

        Assert.True(outcome.Succeeded);
        var members = outcome.Value.State.Content.Members;
        Assert.Equal(["tmp:br01", "tmp:north"], members.Select(member => member.Id).Order(StringComparer.Ordinal));
        Assert.Equal("tmp:north", members.Single(member => member.Key == "BR01").ParentId);
        Assert.Empty(_fixture.Members.All);
    }

    /// <summary>
    /// A commit writes the members with real ids, rebuilds the live version's closure from the new parents, answers the
    /// temporary-to-real map, and advances the semantic revision because the engine reads the live version.
    /// </summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesMembersAndClosureAndMapsTemporaryIds()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(DimensionFixture.DimensionId, Request(SaveMember("tmp:north", "NORTH"), SaveMember("tmp:br01", "BR01", "tmp:north")), Token);

        Assert.True(outcome.Succeeded);
        var north = outcome.Value.IdMap["tmp:north"];
        var branch = outcome.Value.IdMap["tmp:br01"];
        Assert.Equal(north, _fixture.Members.All.Single(member => member.MemberKey == "BR01").ParentDimensionMemberId);
        Assert.Contains(_fixture.Closure.All, row => row.AncestorDimensionMemberId == north && row.DescendantDimensionMemberId == branch && row.Depth == 1);
        Assert.Equal(DimensionFixture.StartingRevision + 1, _fixture.Revision);
        Assert.Equal(branch.ToString(System.Globalization.CultureInfo.InvariantCulture), outcome.Value.State.Content.Members.Single(member => member.Key == "BR01").Id);
    }

    /// <summary>A frozen version accepts no edits; the operation is refused as a conflict and nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task EditOfAFrozenVersion_IsAConflict()
    {
        var outcome = await _fixture.Pipeline().CommitAsync(DimensionFixture.DimensionId, Request(SaveMember("tmp:a", "A") with { VersionId = "102" }), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Empty(_fixture.Members.All);
    }

    /// <summary>A locked profile refuses every edit (M11).</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task LockedProfile_RefusesEveryEdit()
    {
        await _fixture.FollowProfileAsync("calendar");

        var outcome = await _fixture.Pipeline().PreviewAsync(
            DimensionFixture.DimensionId, Request(new DimensionUpdateDimensionOperation { Patch = new DimensionPatch { DisplayName = "Renamed" } }), Token);

        Assert.Equal(DimensionRefusals.ReadOnlyTitle, outcome.Refusal!.Title);
    }

    /// <summary>A new member whose key the version has is refused with the error keyed by the operation and its input.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DuplicateMemberKey_IsAnInputErrorOfItsOperation()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(DimensionFixture.DimensionId, Request(SaveMember("tmp:a", "CA"), SaveMember("tmp:b", "CA")), Token);

        Assert.Equal(RefusalKind.Invalid, outcome.Refusal!.Kind);
        Assert.Equal(["operations[1].input.key"], outcome.Refusal.Errors.Keys);
    }

    /// <summary>Deleting a member with children is blocked, and the refusal carries the coded blockers the delete check answers.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task DeletingAParent_IsBlockedWithItsReasons()
    {
        await _fixture.Members.InsertManyAsync([DimensionRows.Member(1, DimensionFixture.LiveVersionId, "NORTH"), DimensionRows.Member(2, DimensionFixture.LiveVersionId, "BR01", parentId: 1)], Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(
            DimensionFixture.DimensionId, Request(new DimensionDeleteMemberOperation { VersionId = Live, MemberId = "1" }), Token);

        Assert.Equal(RefusalKind.DeleteBlocked, outcome.Refusal!.Kind);
        Assert.Equal([DeleteBlockerCodes.HasChildren], outcome.Refusal.Violations.Select(violation => violation.Code));
    }

    /// <summary>
    /// A preview's findings come from the pending bands, not the stored version: overlapping bands saved in the change set are
    /// reported, yet saving is not blocked, because band rules block only freezing.
    /// </summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task PreviewFindings_ReflectThePendingEdits()
    {
        await _fixture.Dimensions.UpdateAsync(_fixture.Dimensions.All.Single() with { DimensionKind = DimensionKinds.Range }, Token);
        await _fixture.Versions.UpdateAsync(_fixture.Versions.All.Single(version => version.DimensionVersionId == DimensionFixture.LiveVersionId) with { RangeDataType = RangeDataTypes.Decimal }, Token);
        await _fixture.Members.InsertManyAsync([DimensionRows.Member(1, DimensionFixture.LiveVersionId, "LOW"), DimensionRows.Member(2, DimensionFixture.LiveVersionId, "HIGH")], Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(
            DimensionFixture.DimensionId,
            Request(new DimensionSaveBandsOperation { VersionId = Live, Bands = [Band("1", null, 20m), Band("2", 10m, null)] }),
            Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["bands_non_overlapping"], outcome.Value.Violations.Select(violation => violation.Code));
    }

    /// <summary>A role key another dimension has is refused, since role keys are tenant-unique (M6).</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task RoleKeyOfAnotherDimension_IsRefused()
    {
        var outcome = await _fixture.Pipeline().PreviewAsync(DimensionFixture.DimensionId, Request(SaveRole("tmp:r", "Product")), Token);

        Assert.Equal(["operations[0].input.key"], outcome.Refusal!.Errors.Keys);
    }

    /// <summary>A dimension whose only role a report names as a scenario takes no second role (M7).</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task SecondRoleOfAScenarioDimension_IsAConflict()
    {
        await _fixture.Reports.InsertAsync(ReportRows.Report(), Token);
        await _fixture.Scenarios.InsertAsync(new ReportScenarioRecord { ReportId = ReportRows.Report().ReportId, DimensionRoleId = DimensionFixture.RoleId }, Token);

        var outcome = await _fixture.Pipeline().PreviewAsync(DimensionFixture.DimensionId, Request(SaveRole("tmp:r", "originationBranch")), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
    }

    /// <summary>A change set request of operations.</summary>
    private static ChangeSetRequest<DimensionOperation> Request(params DimensionOperation[] operations)
    {
        return new ChangeSetRequest<DimensionOperation> { Operations = operations };
    }

    /// <summary>A <c>saveMember</c> of a member of the live version.</summary>
    private static DimensionSaveMemberOperation SaveMember(string id, string key, string? parentId = null)
    {
        return new DimensionSaveMemberOperation
        {
            VersionId = Live,
            Input = new DimensionMemberInput
            {
                Id = id,
                Key = key,
                DisplayName = key,
                Description = null,
                ParentId = parentId,
                LevelId = null,
                IsAssignable = true,
                SortOrder = null,
                Properties = []
            }
        };
    }

    /// <summary>A <c>saveRole</c>.</summary>
    private static DimensionSaveRoleOperation SaveRole(string id, string key)
    {
        return new DimensionSaveRoleOperation { Input = new DimensionRoleInput { Id = id, Key = key, DisplayName = key, Description = null } };
    }

    /// <summary>A decimal band.</summary>
    private static DimensionRangeBand Band(string memberId, decimal? lower, decimal? upper)
    {
        return new DimensionRangeBand
        {
            Ordinal = 0,
            MemberId = memberId,
            Lower = lower is null ? null : JsonSerializer.SerializeToElement(lower.Value),
            Upper = upper is null ? null : JsonSerializer.SerializeToElement(upper.Value),
            LowerInclusive = true,
            UpperInclusive = false
        };
    }
}

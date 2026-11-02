using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms the dimension lifecycle the manager relies on (blueprint 12): a new dimension starts with a version in the live
/// snapshot; a profile defines its system dimension with the required roles and members; a dimension a frozen snapshot holds cannot
/// be deleted; and a dimension that nothing references is deleted with its versions and roles.
/// </summary>
public sealed class DimensionCommandsTests
{
    /// <summary>The tables.</summary>
    private readonly DimensionFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A created dimension's version is in the live snapshot, its first role exists, and the revision advances.</summary>
    /// <returns>A task that completes when the dimension is checked.</returns>
    [Fact]
    public async Task Create_StartsWithALiveVersion()
    {
        var outcome = await _fixture.Commands().CreateAsync(Input("Region", "region"), Token);

        Assert.True(outcome.Succeeded);
        var version = _fixture.Versions.All.Single(row => row.DimensionVersionId == outcome.Value.VersionId);
        Assert.Equal((outcome.Value.DimensionId, SnapshotRows.LiveId), (version.DimensionId, version.SnapshotId));
        Assert.Equal(DimensionFixture.StartingRevision + 1, _fixture.Revision);
        Assert.Contains(_fixture.Roles.All, role => role.DimensionId == outcome.Value.DimensionId && role.DimensionRoleKey == "region");
    }

    /// <summary>A dimension key or role key another dimension has, ignoring case, is a conflict and nothing is written.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Create_WithATakenRoleKey_IsAConflict()
    {
        var outcome = await _fixture.Commands().CreateAsync(Input("Region", "Branch"), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Single(_fixture.Dimensions.All);
    }

    /// <summary>Defining a profile creates its system dimension with the required roles and members, not removable.</summary>
    /// <returns>A task that completes when the dimension is checked.</returns>
    [Fact]
    public async Task Define_SeedsTheProfilesRequiredKeys()
    {
        var outcome = await _fixture.Commands().DefineAsync(DimensionProfiles.Find("channel")!, Token);

        Assert.True(outcome.Succeeded);
        var dimension = _fixture.Dimensions.All.Single(row => row.DimensionId == outcome.Value.DimensionId);
        Assert.Equal(("channel", DefinitionOrigins.System, false), (dimension.DimensionProfileKey, dimension.DefinitionOrigin, dimension.IsRemovable));
        Assert.Equal(["BRANCH", "MOBILE", "ONLINE"], _fixture.Members.All.Where(row => row.DimensionVersionId == outcome.Value.VersionId).Select(row => row.MemberKey).Order(StringComparer.Ordinal));
    }

    /// <summary>A dimension a frozen snapshot holds cannot be deleted, because restoring that snapshot would need it.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task Delete_IsBlockedByAFrozenSnapshot()
    {
        var refusal = await _fixture.Commands().DeleteAsync(DimensionFixture.DimensionId, Token);

        Assert.Equal([DeleteBlockerCodes.ReferencedBySnapshot], refusal!.Violations.Select(reason => reason.Code));
        Assert.Equal(2, _fixture.Versions.All.Count);
    }

    /// <summary>A dimension nothing references is deleted with its versions and roles.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task Delete_RemovesTheDimensionWithItsVersionsAndRoles()
    {
        var commands = _fixture.Commands();
        var created = (await commands.CreateAsync(Input("Region", "region"), Token)).Value!;

        Assert.Null(await commands.DeleteAsync(created.DimensionId, Token));

        Assert.DoesNotContain(_fixture.Dimensions.All, row => row.DimensionId == created.DimensionId);
        Assert.DoesNotContain(_fixture.Versions.All, row => row.DimensionId == created.DimensionId);
        Assert.DoesNotContain(_fixture.Roles.All, row => row.DimensionId == created.DimensionId);
    }

    /// <summary>A categorical user dimension with a first role.</summary>
    private static CreateDimensionInput Input(string key, string roleKey)
    {
        return new CreateDimensionInput
        {
            Key = key,
            DisplayName = key,
            Kind = DimensionKinds.Categorical,
            RangeDataType = null,
            FirstRole = new DimensionFirstRoleInput { Key = roleKey, DisplayName = roleKey },
            IsEnabled = true,
            IsRemovable = true
        };
    }
}

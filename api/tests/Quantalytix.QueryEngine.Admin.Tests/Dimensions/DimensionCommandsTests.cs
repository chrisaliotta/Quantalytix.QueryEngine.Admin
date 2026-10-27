using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms the dimension lifecycle the manager relies on (blueprint 12, OI-14): a new dimension starts with a live version 1 that
/// the active assumption set pins; a profile defines its system dimension with the required roles and members; freezing the live
/// version continues in a live clone and keeps the note on the frozen one; only a frozen version can be deleted; and a dimension
/// that nothing references is deleted with its versions and pin.
/// </summary>
public sealed class DimensionCommandsTests
{
    /// <summary>The tables.</summary>
    private readonly DimensionFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A created dimension's version 1 is live, the active set pins it, and its first role exists.</summary>
    /// <returns>A task that completes when the dimension is checked.</returns>
    [Fact]
    public async Task Create_StartsWithAPinnedLiveVersion()
    {
        var outcome = await _fixture.Commands().CreateAsync(Input("Region", "region"), Token);

        Assert.True(outcome.Succeeded);
        var version = _fixture.Versions.All.Single(row => row.DimensionVersionId == outcome.Value.VersionId);
        Assert.Equal((1, PublicationStatuses.Published), (version.VersionNumber, version.Status));
        Assert.Contains(_fixture.Pins.All, pin => pin.DimensionId == outcome.Value.DimensionId && pin.DimensionVersionId == version.DimensionVersionId);
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

    /// <summary>
    /// Freezing the live version retires it with the note and its retirement time, and continues in a live version 2 holding a
    /// copy of its members; the frozen version can then be deleted, the live one cannot.
    /// </summary>
    /// <returns>A task that completes when the versions are checked.</returns>
    [Fact]
    public async Task Freeze_ContinuesInALiveClone()
    {
        await _fixture.Members.InsertAsync(DimensionRows.Member(1, DimensionFixture.LiveVersionId, "NORTH"), Token);
        var commands = _fixture.Commands();

        var outcome = await commands.FreezeAsync(DimensionFixture.LiveVersionId, " FY26 close ", Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(2, outcome.Value.Live.VersionNumber);
        Assert.Equal(1, outcome.Value.Live.MemberCount);
        var frozen = _fixture.Versions.All.Single(row => row.DimensionVersionId == DimensionFixture.LiveVersionId);
        Assert.Equal((PublicationStatuses.Retired, "FY26 close"), (frozen.Status, frozen.FreezeNote));
        Assert.NotNull(frozen.RetiredAt);
        Assert.Null(await commands.DeleteVersionAsync(DimensionFixture.LiveVersionId, Token));
        var live = _fixture.Versions.All.Single(row => row.Status == PublicationStatuses.Published).DimensionVersionId;
        Assert.Equal(RefusalKind.DeleteBlocked, (await commands.DeleteVersionAsync(live, Token))!.Kind);
    }

    /// <summary>A dimension nothing references is deleted with its versions, roles and active-set pin.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task Delete_RemovesTheDimensionWithItsPin()
    {
        var commands = _fixture.Commands();
        var created = (await commands.CreateAsync(Input("Region", "region"), Token)).Value!;

        Assert.Null(await commands.DeleteAsync(created.DimensionId, Token));

        Assert.DoesNotContain(_fixture.Dimensions.All, row => row.DimensionId == created.DimensionId);
        Assert.DoesNotContain(_fixture.Versions.All, row => row.DimensionId == created.DimensionId);
        Assert.DoesNotContain(_fixture.Pins.All, pin => pin.DimensionId == created.DimensionId);
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

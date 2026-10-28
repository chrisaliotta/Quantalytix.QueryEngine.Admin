using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// Confirms the published members pickers read (blueprint 10): only the live version's members, named by keys, in hierarchy order
/// so a picker can indent them without sorting, and nothing for a dimension without a live version (the endpoint's 404).
/// </summary>
public sealed class PublishedMembersTests
{
    /// <summary>Members come from the live version, each followed by its descendants, siblings by sort order, with parent and level keys.</summary>
    /// <returns>A task that completes when the members are checked.</returns>
    [Fact]
    public async Task LiveVersionMembers_InHierarchyOrder()
    {
        var published = await new TenantTables().PublishedMembers().ReadAsync(100, TestContext.Current.CancellationToken);

        Assert.Equal(102, published!.VersionId);
        Assert.Equal(
            [
                new PublishedMember { Key = "HQ", DisplayName = "HQ", ParentKey = null, IsAssignable = true, LevelKey = null },
                new PublishedMember { Key = "EAST", DisplayName = "EAST", ParentKey = null, IsAssignable = true, LevelKey = null },
                new PublishedMember { Key = "E2", DisplayName = "E2", ParentKey = "EAST", IsAssignable = true, LevelKey = "branch" },
                new PublishedMember { Key = "E1", DisplayName = "E1", ParentKey = "EAST", IsAssignable = false, LevelKey = "branch" }
            ],
            published.Members);
    }

    /// <summary>A dimension whose versions are all retired has no published members.</summary>
    /// <returns>A task that completes when the members are checked.</returns>
    [Fact]
    public async Task NoLiveVersion_HasNoPublishedMembers()
    {
        var tables = new TenantTables();
        await tables.DimensionVersions.DeleteAsync(new { DimensionVersionId = 102L }, TestContext.Current.CancellationToken);

        Assert.Null(await tables.PublishedMembers().ReadAsync(100, TestContext.Current.CancellationToken));
    }
}

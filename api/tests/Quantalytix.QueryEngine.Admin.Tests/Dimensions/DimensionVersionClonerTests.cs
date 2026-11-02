using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that cloning a dimension version, as creating a report dimension from a template and copying into a snapshot do, copies
/// its content into the target version with every internal reference remapped: levels; members with their parents and levels;
/// range bands with their members; and the closure of the copied tree. A clone that pointed back into the source version would let
/// edits to live corrupt a frozen snapshot.
/// </summary>
public sealed class DimensionVersionClonerTests
{
    /// <summary>The version being cloned.</summary>
    private const long SourceVersionId = 10;

    /// <summary>The new version the clone fills.</summary>
    private const long TargetVersionId = 11;

    /// <summary>The levels table: two levels of the source.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        Level(100, SourceVersionId, "country", 1),
        Level(101, SourceVersionId, "state", 2));

    /// <summary>The members table: US over CA and NY in the source, plus a member of another version.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        Member(200, SourceVersionId, "US", dimensionLevelId: 100),
        Member(201, SourceVersionId, "CA", parentId: 200, dimensionLevelId: 101),
        Member(202, SourceVersionId, "NY", parentId: 200, dimensionLevelId: 101),
        Member(999, 9, "TX"));

    /// <summary>The range bands table: two bands of the source.</summary>
    private readonly InMemoryTableGateway<RangeBandRecord> _bands = new(
        DecimalBand(SourceVersionId, 1, 201, null, 10m),
        DecimalBand(SourceVersionId, 2, 202, 10m, null));

    /// <summary>The closure table, empty until the clone writes the target's.</summary>
    private readonly InMemoryTableGateway<DimensionClosureRecord> _closure = new();

    /// <summary>Levels and members are copied under new IDs, and each copied member's parent and level are the copies of the originals.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task LevelsAndMembers_AreCopiedWithParentsAndLevelsRemapped()
    {
        await CloneAsync();

        var levels = _levels.All.Where(level => level.DimensionVersionId == TargetVersionId).ToDictionary(level => level.LevelKey);
        var members = _members.All.Where(member => member.DimensionVersionId == TargetVersionId).ToDictionary(member => member.MemberKey);
        Assert.Equal(["country", "state"], levels.Keys.Order());
        Assert.Equal(["CA", "NY", "US"], members.Keys.Order());
        Assert.DoesNotContain(levels.Values, level => level.DimensionLevelId is 100 or 101);
        Assert.DoesNotContain(members.Values, member => member.DimensionMemberId is 200 or 201 or 202);
        Assert.Null(members["US"].ParentDimensionMemberId);
        Assert.Equal(members["US"].DimensionMemberId, members["CA"].ParentDimensionMemberId);
        Assert.Equal(members["US"].DimensionMemberId, members["NY"].ParentDimensionMemberId);
        Assert.Equal(levels["country"].DimensionLevelId, members["US"].DimensionLevelId);
        Assert.Equal(levels["state"].DimensionLevelId, members["CA"].DimensionLevelId);
        Assert.Equal(3, _members.All.Count(member => member.DimensionVersionId == SourceVersionId));
    }

    /// <summary>Range bands keep their ordinals and bounds and assign the copies of their members.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task RangeBands_AssignTheCopiedMembers()
    {
        await CloneAsync();

        var memberIds = _members.All.Where(member => member.DimensionVersionId == TargetVersionId).ToDictionary(member => member.MemberKey, member => member.DimensionMemberId);
        var bands = _bands.All.Where(band => band.DimensionVersionId == TargetVersionId).OrderBy(band => band.Ordinal).ToList();
        Assert.Equal(
            [DecimalBand(TargetVersionId, 1, memberIds["CA"], null, 10m), DecimalBand(TargetVersionId, 2, memberIds["NY"], 10m, null)],
            bands);
    }

    /// <summary>The target gets the closure of its copied tree, so the engine can group its members as soon as it is live.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Closure_IsWrittenForTheCopiedTree()
    {
        await CloneAsync();

        var memberIds = _members.All.Where(member => member.DimensionVersionId == TargetVersionId).ToDictionary(member => member.MemberKey, member => member.DimensionMemberId);
        (long Ancestor, long Descendant, int Depth)[] expected =
        [
            (memberIds["US"], memberIds["US"], 0), (memberIds["CA"], memberIds["CA"], 0), (memberIds["NY"], memberIds["NY"], 0),
            (memberIds["US"], memberIds["CA"], 1), (memberIds["US"], memberIds["NY"], 1)
        ];
        Assert.Equal(expected.Order(), _closure.All.Select(row => (row.AncestorDimensionMemberId, row.DescendantDimensionMemberId, row.Depth)).Order());
        Assert.All(_closure.All, row => Assert.Equal(TargetVersionId, row.DimensionVersionId));
    }

    /// <summary>Clones the source into the target with this test's tables.</summary>
    private Task CloneAsync()
    {
        var cloner = new DimensionVersionCloner(_levels, _members, _bands, _closure, new IdGenerator());
        return cloner.CloneAsync(SourceVersionId, TargetVersionId, TestContext.Current.CancellationToken);
    }
}

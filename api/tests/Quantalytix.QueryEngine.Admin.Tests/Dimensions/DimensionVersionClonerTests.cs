using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that cloning a dimension version copies its content into the new draft with every internal reference remapped:
/// levels; members with their parents and levels; and range bands with their members. A clone that pointed back into the
/// source version would let edits to the draft corrupt published content.
/// </summary>
public sealed class DimensionVersionClonerTests
{
    /// <summary>The cloned dimension.</summary>
    private const long DimensionId = 1;

    /// <summary>The version being cloned.</summary>
    private const long SourceVersionId = 10;

    /// <summary>The new draft version the clone fills.</summary>
    private const long TargetVersionId = 11;

    /// <summary>The levels table: two levels of the source.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        Level(100, SourceVersionId, "country", 1),
        Level(101, SourceVersionId, "state", 2));

    /// <summary>The members table: US over CA and NY in the source, plus a member of another version.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        Member(200, SourceVersionId, "US", levelId: 100),
        Member(201, SourceVersionId, "CA", parentId: 200, levelId: 101),
        Member(202, SourceVersionId, "NY", parentId: 200, levelId: 101),
        Member(999, 9, "TX"));

    /// <summary>The range bands table: two bands of the source.</summary>
    private readonly InMemoryTableGateway<RangeBandRecord> _bands = new(
        DecimalBand(SourceVersionId, 1, 201, null, 10m),
        DecimalBand(SourceVersionId, 2, 202, 10m, null));

    /// <summary>The new draft version.</summary>
    private readonly DimensionVersionRecord _target = Version(TargetVersionId, DimensionId, 3, PublicationStatuses.Draft);

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
        Assert.DoesNotContain(levels.Values, level => level.LevelId is 100 or 101);
        Assert.DoesNotContain(members.Values, member => member.MemberId is 200 or 201 or 202);
        Assert.Null(members["US"].ParentMemberId);
        Assert.Equal(members["US"].MemberId, members["CA"].ParentMemberId);
        Assert.Equal(members["US"].MemberId, members["NY"].ParentMemberId);
        Assert.Equal(levels["country"].LevelId, members["US"].LevelId);
        Assert.Equal(levels["state"].LevelId, members["CA"].LevelId);
        Assert.Equal(3, _members.All.Count(member => member.DimensionVersionId == SourceVersionId));
    }

    /// <summary>Range bands keep their ordinals and bounds and assign the copies of their members.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task RangeBands_AssignTheCopiedMembers()
    {
        await CloneAsync();

        var memberIds = _members.All.Where(member => member.DimensionVersionId == TargetVersionId).ToDictionary(member => member.MemberKey, member => member.MemberId);
        var bands = _bands.All.Where(band => band.DimensionVersionId == TargetVersionId).OrderBy(band => band.Ordinal).ToList();
        Assert.Equal(
            [DecimalBand(TargetVersionId, 1, memberIds["CA"], null, 10m), DecimalBand(TargetVersionId, 2, memberIds["NY"], 10m, null)],
            bands);
    }

    /// <summary>Clones the source into the target with this test's tables.</summary>
    private Task CloneAsync()
    {
        var cloner = new DimensionVersionCloner(_levels, _members, _bands, new IdGenerator());
        return cloner.CloneAsync(SourceVersionId, _target, TestContext.Current.CancellationToken);
    }
}

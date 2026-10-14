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
/// levels; members with their parents and levels; range bands with their members; and, for each mapping set of the
/// dimension, a new draft of every non-retired mapping version pinned to the source, numbered after the set's versions and
/// with its entries following their member keys. A clone that pointed back into the source version would let edits to the
/// draft corrupt published content, and mapped bindings resolve through the mapping version of the new dimension version.
/// </summary>
public sealed class DimensionVersionClonerTests
{
    /// <summary>The cloned dimension.</summary>
    private const long DimensionId = 1;

    /// <summary>The version being cloned.</summary>
    private const long SourceVersionId = 10;

    /// <summary>The new draft version the clone fills.</summary>
    private const long TargetVersionId = 11;

    /// <summary>A mapping set of the dimension with a version pinned to the source.</summary>
    private const long BranchCodesSetId = 300;

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

    /// <summary>The mapping sets table: two sets of the dimension and one of another dimension.</summary>
    private readonly InMemoryTableGateway<MappingSetRecord> _mappingSets = new(
        MappingSet(BranchCodesSetId, DimensionId),
        MappingSet(301, DimensionId),
        MappingSet(302, 2));

    /// <summary>
    /// The mapping versions table: set 300 has v1 pinned to the source and v2 pinned to another version; set 301's version
    /// pinned to the source is retired; set 302 belongs to another dimension.
    /// </summary>
    private readonly InMemoryTableGateway<MappingVersionRecord> _mappingVersions = new(
        MappingVersion(400, BranchCodesSetId, 1, SourceVersionId, PublicationStatuses.Published),
        MappingVersion(401, BranchCodesSetId, 2, 9, PublicationStatuses.Published),
        MappingVersion(402, 301, 1, SourceVersionId, PublicationStatuses.Retired),
        MappingVersion(403, 302, 1, SourceVersionId, PublicationStatuses.Published));

    /// <summary>The entries table: three entries of mapping version 400, one of them pointing outside the source version.</summary>
    private readonly InMemoryTableGateway<DimensionMappingRecord> _entries = new(
        Entry(500, 400, "06", 201),
        Entry(501, 400, "36", 202),
        Entry(502, 400, "48", 999));

    /// <summary>The new draft version.</summary>
    private readonly DimensionVersionRecord _target = Version(TargetVersionId, DimensionId, 3, PublicationStatuses.Draft, null, null);

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

    /// <summary>
    /// Only set 300's non-retired mapping version pinned to the source is cloned: as a draft v3 pinned to the new version, with
    /// each entry pointing at the new member of the same key, and the entry whose member is outside the source dropped.
    /// </summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task MappingVersions_AreClonedAsDraftsWithEntriesRemappedByMemberKey()
    {
        await CloneAsync();

        var clone = Assert.Single(_mappingVersions.All, version => version.DimensionVersionId == TargetVersionId);
        Assert.Equal(BranchCodesSetId, clone.MappingSetId);
        Assert.Equal(3, clone.VersionNumber);
        Assert.Equal(PublicationStatuses.Draft, clone.Status);
        Assert.Null(clone.PublishedAt);
        Assert.NotEqual(400, clone.MappingVersionId);

        var membersById = _members.All.ToDictionary(member => member.MemberId);
        var clonedEntries = _entries.All.Where(entry => entry.MappingVersionId == clone.MappingVersionId).ToList();
        var targets = clonedEntries.ToDictionary(entry => entry.SourceKeyValue, entry => membersById[entry.MemberId]);
        Assert.Equal(["06", "36"], targets.Keys.Order());
        Assert.Equal(("CA", TargetVersionId), (targets["06"].MemberKey, targets["06"].DimensionVersionId));
        Assert.Equal(("NY", TargetVersionId), (targets["36"].MemberKey, targets["36"].DimensionVersionId));
        Assert.DoesNotContain(clonedEntries, entry => entry.MappingEntryId is 500 or 501);
        Assert.Equal(3, _entries.All.Count(entry => entry.MappingVersionId == 400));
    }

    /// <summary>Clones the source into the target with this test's tables.</summary>
    private Task CloneAsync()
    {
        var cloner = new DimensionVersionCloner(_levels, _members, _bands, _mappingSets, _mappingVersions, _entries, new IdGenerator());
        return cloner.CloneAsync(SourceVersionId, _target, TestContext.Current.CancellationToken);
    }

    /// <summary>A mapping set of a dimension.</summary>
    private static MappingSetRecord MappingSet(long mappingSetId, long dimensionId)
    {
        return new MappingSetRecord
        {
            MappingSetId = mappingSetId,
            MappingSetKey = $"zz_dim_codes_{mappingSetId}",
            SourceNamespace = "core_banking",
            DimensionId = dimensionId,
            DisplayName = $"Codes {mappingSetId}"
        };
    }

    /// <summary>A mapping version of a set pinned to a dimension version.</summary>
    private static MappingVersionRecord MappingVersion(long mappingVersionId, long mappingSetId, int versionNumber, long dimensionVersionId, string status)
    {
        return new MappingVersionRecord
        {
            MappingVersionId = mappingVersionId,
            MappingSetId = mappingSetId,
            VersionNumber = versionNumber,
            DimensionVersionId = dimensionVersionId,
            Status = status,
            PublishedAt = Utc(2026, 1, 1)
        };
    }

    /// <summary>A mapping entry from a source value to a member.</summary>
    private static DimensionMappingRecord Entry(long mappingEntryId, long mappingVersionId, string sourceKeyValue, long memberId)
    {
        return new DimensionMappingRecord
        {
            MappingEntryId = mappingEntryId,
            MappingVersionId = mappingVersionId,
            SourceKeyValue = sourceKeyValue,
            MemberId = memberId
        };
    }
}

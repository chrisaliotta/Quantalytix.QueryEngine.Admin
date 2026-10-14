using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;
using static Quantalytix.QueryEngine.Admin.Tests.Mappings.MappingRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms that creating a mapping version as a clone moves the source version's entries onto another dimension version by
/// member key: each copied entry keeps its exact source value under a new entry ID and points at the member with the same
/// key in the new draft's dimension version, while an entry whose key has no member there, or several, is dropped and its
/// source value reported. Member IDs differ between dimension versions, so a clone that kept them would map facts to the
/// members of the old version.
/// </summary>
public sealed class MappingVersionClonerTests
{
    /// <summary>The mapping set being versioned.</summary>
    private const long MappingSetId = 300;

    /// <summary>The published mapping version that is cloned.</summary>
    private const long SourceMappingVersionId = 400;

    /// <summary>The dimension version the source mapping version pins.</summary>
    private const long OldVersionId = 10;

    /// <summary>The dimension version the clone pins.</summary>
    private const long NewVersionId = 11;

    /// <summary>The first ID the generator hands out, above every seeded ID.</summary>
    private const long FirstNewId = 1000;

    /// <summary>
    /// The members: the old version has CA, NY, TX and NV; the new version has CA and NY under new IDs, no TX, and NV twice,
    /// as a draft may before validation.
    /// </summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        Member(101, OldVersionId, "CA"),
        Member(102, OldVersionId, "NY"),
        Member(103, OldVersionId, "TX"),
        Member(104, OldVersionId, "NV"),
        Member(201, NewVersionId, "CA"),
        Member(202, NewVersionId, "NY"),
        Member(204, NewVersionId, "NV"),
        Member(205, NewVersionId, "NV"));

    /// <summary>The mapping versions: the cloned v1 and a draft v2 of the set.</summary>
    private readonly InMemoryTableGateway<MappingVersionRecord> _versions = new(
        MappingVersion(SourceMappingVersionId, MappingSetId, 1, OldVersionId, PublicationStatuses.Published),
        MappingVersion(401, MappingSetId, 2, 12, PublicationStatuses.Draft));

    /// <summary>The entries of v1: two source values onto CA (one with a leading zero), one onto NY, and one each onto TX and NV.</summary>
    private readonly InMemoryTableGateway<DimensionMappingRecord> _entries = new(
        Entry(500, SourceMappingVersionId, "06", 101),
        Entry(501, SourceMappingVersionId, "006", 101),
        Entry(502, SourceMappingVersionId, "36", 102),
        Entry(503, SourceMappingVersionId, "48", 103),
        Entry(504, SourceMappingVersionId, "32", 104));

    /// <summary>
    /// The clone is draft v3 pinned to the new version, and every entry whose member key the new version holds once points at
    /// that version's member under a new entry ID; the source's entries are untouched.
    /// </summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_RemapsEntriesByMemberKey()
    {
        var created = await CloneAsync();

        Assert.Equal(
            MappingVersion(FirstNewId, MappingSetId, 3, NewVersionId, PublicationStatuses.Draft),
            created.Version);
        Assert.Equal(created.Version, _versions.All.Single(version => version.MappingVersionId == created.Version.MappingVersionId));
        var cloned = _entries.All.Where(entry => entry.MappingVersionId == created.Version.MappingVersionId).ToList();
        Assert.Equal(
            [("006", 201L), ("06", 201L), ("36", 202L)],
            cloned.Select(entry => (entry.SourceKeyValue, entry.MemberId)).Order());
        Assert.All(cloned, entry => Assert.True(entry.MappingEntryId > FirstNewId));
        Assert.Equal(5, _entries.All.Count(entry => entry.MappingVersionId == SourceMappingVersionId));
    }

    /// <summary>The entries onto TX, which the new version lacks, and onto NV, which it holds twice, are dropped and reported in ordinal order.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_ReportsTheSourceKeysItDropped()
    {
        var created = await CloneAsync();

        Assert.Equal(["32", "48"], created.DroppedSourceKeys);
        Assert.DoesNotContain(
            _entries.All,
            entry => entry.MappingVersionId == created.Version.MappingVersionId && entry.SourceKeyValue is "32" or "48");
    }

    /// <summary>Creates the set's next version pinned to the new dimension version as a clone of v1, with this test's tables.</summary>
    private Task<CreatedMappingVersion> CloneAsync()
    {
        var ids = new SequentialIdGenerator(FirstNewId);
        var service = new MappingVersionService(_versions, _entries, new MappingVersionCloner(_entries, _members, ids), ids);
        var input = new NewMappingVersionInput { DimensionVersionId = NewVersionId, CloneFromVersionId = SourceMappingVersionId };
        return service.CreateAsync(MappingSetId, input, TestContext.Current.CancellationToken);
    }
}

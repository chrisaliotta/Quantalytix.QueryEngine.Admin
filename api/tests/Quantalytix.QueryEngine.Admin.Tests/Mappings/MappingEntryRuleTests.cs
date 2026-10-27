using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;
using static Quantalytix.QueryEngine.Admin.Tests.Mappings.MappingRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the entry rules of mapping versions: <c>source_key_unique</c> compares source values exactly, so values that
/// differ only by leading zeros are distinct while a repeated value is reported once; <c>entry_member_in_version</c> reports
/// entries whose member key the published version of the set's dimension lacks; <c>entry_member_assignable</c> reports
/// entries that map to a folder-only member; and <c>dimension_version_published</c> reports a dimension with no published
/// version, which the member rules then leave alone. The engine maps each fact's source value through these entries in the
/// dimension version the active assumption set pins, which is the published one.
/// </summary>
public sealed class MappingEntryRuleTests
{
    /// <summary>The mapping version being validated.</summary>
    private const long MappingVersionId = 40;

    /// <summary>The mapping set the version belongs to.</summary>
    private const long MappingSetId = 300;

    /// <summary>The set's dimension.</summary>
    private const long DimensionId = 5;

    /// <summary>The members: US (folder only) over CA and NY in the published version 10, and TX only in the draft version 11.</summary>
    private static readonly DimensionMemberRecord[] Members =
    [
        Member(1, 10, "US") with { IsAssignable = false },
        Member(2, 10, "CA", parentId: 1),
        Member(3, 10, "NY", parentId: 1),
        Member(9, 11, "TX")
    ];

    /// <summary>Source values that differ only by leading zeros are different values, so both may map.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceKeysDifferingOnlyByLeadingZeros_AreDistinct()
    {
        var rule = new SourceKeyUniqueRule(Entries(Entry(1, MappingVersionId, "003000", "CA"), Entry(2, MappingVersionId, "3000", "NY")));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        Assert.Empty(violations);
    }

    /// <summary>A source value used by two entries is reported once, naming the value; entries of other versions do not count.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedSourceKey_IsReportedOnce()
    {
        var rule = new SourceKeyUniqueRule(Entries(
            Entry(1, MappingVersionId, "06", "CA"),
            Entry(2, MappingVersionId, "06", "NY"),
            Entry(3, MappingVersionId, "36", "NY"),
            Entry(4, 41, "36", "NY")));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(SourceKeyUniqueRule.Code, violation.Code);
        Assert.Equal("Source key '06' appears in 2 entries.", violation.Message);
    }

    /// <summary>An entry whose member key only a draft dimension version has is reported, naming its source value and key.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MemberMissingFromThePublishedVersion_IsNotInTheVersion()
    {
        var rule = new EntryMemberInVersionRule(Reader(Entry(1, MappingVersionId, "06", "CA"), Entry(2, MappingVersionId, "48", "TX")));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberInVersionRule.Code, violation.Code);
        Assert.Equal("Source key '48' maps to member 'TX', which is not a member of v1, the published version of the set's dimension.", violation.Message);
    }

    /// <summary>
    /// An entry that maps to a folder-only member is reported; an entry outside the version is left to
    /// <c>entry_member_in_version</c>, so it is not reported twice.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task FolderOnlyMember_IsNotAssignable()
    {
        var rule = new EntryMemberAssignableRule(Reader(
            Entry(1, MappingVersionId, "06", "CA"),
            Entry(2, MappingVersionId, "00", "US"),
            Entry(3, MappingVersionId, "48", "TX")));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberAssignableRule.Code, violation.Code);
        Assert.Equal("Source key '00' maps to member 'US', which is not assignable.", violation.Message);
    }

    /// <summary>
    /// While the set's dimension has no published version, <c>dimension_version_published</c> names the cause once and the
    /// member rules report nothing, rather than every entry.
    /// </summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task DimensionWithoutPublishedVersion_IsReportedOnce()
    {
        var versions = new InMemoryTableGateway<DimensionVersionRecord>(Version(11, DimensionId, 2, PublicationStatuses.Draft));
        var reader = Reader(versions, Entry(1, MappingVersionId, "48", "TX"));
        var published = new DimensionVersionPublishedRule(MappingVersions(), Sets(), versions, new InMemoryTableGateway<DimensionRecord>(Dimension(DimensionId, DimensionKinds.Categorical)));

        var violation = Assert.Single(await published.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken));
        Assert.Equal(DimensionVersionPublishedRule.Code, violation.Code);
        Assert.Empty(await new EntryMemberInVersionRule(reader).EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken));
        Assert.Empty(await new EntryMemberAssignableRule(reader).EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken));
    }

    /// <summary>An entries table holding the given rows.</summary>
    private static InMemoryTableGateway<DimensionMappingRecord> Entries(params DimensionMappingRecord[] entries)
    {
        return new InMemoryTableGateway<DimensionMappingRecord>(entries);
    }

    /// <summary>The mapping versions table: the validated draft.</summary>
    private static InMemoryTableGateway<MappingVersionRecord> MappingVersions()
    {
        return new InMemoryTableGateway<MappingVersionRecord>(MappingVersion(MappingVersionId, MappingSetId, 1, PublicationStatuses.Draft));
    }

    /// <summary>The mapping sets table: the set onto the dimension.</summary>
    private static InMemoryTableGateway<MappingSetRecord> Sets()
    {
        return new InMemoryTableGateway<MappingSetRecord>(MappingSet(MappingSetId, DimensionId));
    }

    /// <summary>An entry set reader over the dimension's published version 10 and draft version 11.</summary>
    private static MappingEntrySetReader Reader(params DimensionMappingRecord[] entries)
    {
        var versions = new InMemoryTableGateway<DimensionVersionRecord>(
            Version(10, DimensionId, 1, PublicationStatuses.Published),
            Version(11, DimensionId, 2, PublicationStatuses.Draft));
        return Reader(versions, entries);
    }

    /// <summary>An entry set reader over the given dimension versions, with the members and the given entries.</summary>
    private static MappingEntrySetReader Reader(InMemoryTableGateway<DimensionVersionRecord> versions, params DimensionMappingRecord[] entries)
    {
        var dimensions = new PublishedDimensionReader(
            versions, new InMemoryTableGateway<DimensionLevelRecord>(), new InMemoryTableGateway<DimensionMemberRecord>(Members), new PublicationSet());
        return new MappingEntrySetReader(MappingVersions(), Sets(), Entries(entries), dimensions);
    }
}

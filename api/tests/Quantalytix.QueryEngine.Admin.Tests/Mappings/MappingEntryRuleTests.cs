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
/// entries whose member belongs to another dimension version; <c>entry_member_assignable</c> reports entries that map to a
/// folder-only member. The engine maps each fact's source value through these entries, so a repeated value would map one
/// value to two members, and a foreign or folder member would classify facts outside the version's hierarchy.
/// </summary>
public sealed class MappingEntryRuleTests
{
    /// <summary>The mapping version being validated.</summary>
    private const long MappingVersionId = 40;

    /// <summary>The dimension version the mapping version pins.</summary>
    private const long PinnedVersionId = 10;

    /// <summary>The members: US (folder only) over CA and NY in the pinned version, and TX in another version.</summary>
    private static readonly DimensionMemberRecord[] Members =
    [
        Member(1, PinnedVersionId, "US") with { IsAssignable = false },
        Member(2, PinnedVersionId, "CA", parentId: 1),
        Member(3, PinnedVersionId, "NY", parentId: 1),
        Member(9, 11, "TX")
    ];

    /// <summary>Source values that differ only by leading zeros are different values, so both may map.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceKeysDifferingOnlyByLeadingZeros_AreDistinct()
    {
        var rule = new SourceKeyUniqueRule(Entries(Entry(1, MappingVersionId, "003000", 2), Entry(2, MappingVersionId, "3000", 3)));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        Assert.Empty(violations);
    }

    /// <summary>A source value used by two entries is reported once, naming the value; entries of other versions do not count.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedSourceKey_IsReportedOnce()
    {
        var rule = new SourceKeyUniqueRule(Entries(
            Entry(1, MappingVersionId, "06", 2),
            Entry(2, MappingVersionId, "06", 3),
            Entry(3, MappingVersionId, "36", 3),
            Entry(4, 41, "36", 3)));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(SourceKeyUniqueRule.Code, violation.Rule);
        Assert.Equal("Source key '06' appears in 2 entries.", violation.Message);
    }

    /// <summary>An entry that maps to a member of another dimension version is reported, naming its source value.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MemberOfAnotherVersion_IsNotInTheVersion()
    {
        var rule = new EntryMemberInVersionRule(Reader(Entry(1, MappingVersionId, "06", 2), Entry(2, MappingVersionId, "48", 9)));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberInVersionRule.Code, violation.Rule);
        Assert.Equal("Source key '48' maps to member 9, which is not a member of the pinned dimension version.", violation.Message);
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
            Entry(1, MappingVersionId, "06", 2),
            Entry(2, MappingVersionId, "00", 1),
            Entry(3, MappingVersionId, "48", 9)));

        var violations = await rule.EvaluateAsync(MappingVersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberAssignableRule.Code, violation.Rule);
        Assert.Equal("Source key '00' maps to member 'US', which is not assignable.", violation.Message);
    }

    /// <summary>An entries table holding the given rows.</summary>
    private static InMemoryTableGateway<DimensionMappingRecord> Entries(params DimensionMappingRecord[] entries)
    {
        return new InMemoryTableGateway<DimensionMappingRecord>(entries);
    }

    /// <summary>An entry set reader over the mapping version, pinned to the pinned version, with the given entries and the members.</summary>
    private static MappingEntrySetReader Reader(params DimensionMappingRecord[] entries)
    {
        return new MappingEntrySetReader(
            new InMemoryTableGateway<MappingVersionRecord>(MappingVersion(MappingVersionId, 300, 1, PinnedVersionId, PublicationStatuses.Draft)),
            Entries(entries),
            new InMemoryTableGateway<DimensionMemberRecord>(Members));
    }
}

using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Mappings.MappingRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms the entry rules of mapping versions over a loaded snapshot: <c>source_key_unique</c> compares source values exactly, so
/// values that differ only by leading zeros are distinct while a repeated value is reported on the repeating entry;
/// <c>entry_member_in_version</c> reports entries whose member key the live version of the set's dimension lacks;
/// <c>entry_member_assignable</c> reports entries that map to a folder-only member; and <c>dimension_version_published</c> reports a
/// dimension with no live version, which the member rules then leave alone. The engine maps each fact's source value through these
/// entries in the dimension version the active assumption set pins, which is the live one.
/// </summary>
public sealed class MappingEntryRuleTests
{
    /// <summary>Source values that differ only by leading zeros are different values, so both may map.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SourceKeysDifferingOnlyByLeadingZeros_AreDistinct()
    {
        var violations = await EvaluateAsync(new SourceKeyUniqueRule(), Entry(402, MappingsFixture.LiveMappingVersionId, "006", "NY"));

        Assert.Empty(violations);
    }

    /// <summary>A source value used by two entries is reported once, on the second entry, so the editor flags the row to fix.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedSourceKey_IsReportedOnTheRepeatingEntry()
    {
        var violations = await EvaluateAsync(new SourceKeyUniqueRule(), Entry(402, MappingsFixture.LiveMappingVersionId, "06", "NY"));

        var violation = Assert.Single(violations);
        Assert.Equal(SourceKeyUniqueRule.Code, violation.Code);
        Assert.Equal("Source key '06' appears in 2 entries.", violation.Message);
        Assert.Equal(new ViolationTarget { Area = MappingAreas.Entries, ItemId = "402", Filter = "06" }, violation.Target);
    }

    /// <summary>An entry whose member key only a frozen dimension version has is reported, naming its source value and key.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MemberMissingFromTheLiveVersion_IsNotInTheVersion()
    {
        var violations = await EvaluateAsync(new EntryMemberInVersionRule(), Entry(402, MappingsFixture.LiveMappingVersionId, "48", "TX"));

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberInVersionRule.Code, violation.Code);
        Assert.Equal("Source key '48' maps to member 'TX', which is not a member of v2, the live version of the set's dimension.", violation.Message);
    }

    /// <summary>
    /// An entry that maps to a folder-only member is reported; an entry outside the version is left to <c>entry_member_in_version</c>,
    /// so it is not reported twice.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task FolderOnlyMember_IsNotAssignable()
    {
        var violations = await EvaluateAsync(
            new EntryMemberAssignableRule(),
            Entry(402, MappingsFixture.LiveMappingVersionId, "00", "US"),
            Entry(403, MappingsFixture.LiveMappingVersionId, "48", "TX"));

        var violation = Assert.Single(violations);
        Assert.Equal(EntryMemberAssignableRule.Code, violation.Code);
        Assert.Equal("Source key '00' maps to member 'US', which is not assignable.", violation.Message);
    }

    /// <summary>
    /// While the set's dimension has no live version, <c>dimension_version_published</c> names the cause once and the member rules
    /// report nothing, rather than every entry.
    /// </summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task DimensionWithoutLiveVersion_IsReportedOnce()
    {
        var fixture = new MappingsFixture();
        await fixture.DimensionVersions.DeleteAsync(new { DimensionVersionId = 10L }, TestContext.Current.CancellationToken);
        var snapshot = await fixture.MappingLoader().LoadLiveAsync(MappingsFixture.MappingSetId, TestContext.Current.CancellationToken);

        var violations = await MappingsFixture.MappingRules().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.Equal([DimensionVersionPublishedRule.Code], violations.Select(violation => violation.Code));
    }

    /// <summary>Runs a rule over the live version of mapping set 300 with extra entries.</summary>
    private static async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ISnapshotRule<MappingSetSnapshot> rule, params DimensionMappingRecord[] added)
    {
        var fixture = new MappingsFixture();
        await fixture.Entries.InsertManyAsync(added, TestContext.Current.CancellationToken);
        var snapshot = await fixture.MappingLoader().LoadLiveAsync(MappingsFixture.MappingSetId, TestContext.Current.CancellationToken);
        return await rule.EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }
}

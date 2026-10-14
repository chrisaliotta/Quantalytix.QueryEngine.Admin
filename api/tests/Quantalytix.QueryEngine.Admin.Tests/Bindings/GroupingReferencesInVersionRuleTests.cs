using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>grouping_references_in_version</c> checks a binding's grouping level, grouping ancestor and unknown
/// member against the dimension version the binding resolves: the pinned version when it pins one, otherwise the published
/// version whose validity window contains now, ignoring earlier published versions and drafts. An unpinned binding whose
/// dimension has no version in effect is reported too. The query engine binds these references by raw ID within the
/// resolved version, so a reference into another version groups or routes nothing.
/// </summary>
public sealed class GroupingReferencesInVersionRuleTests
{
    /// <summary>The dataset version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The bound dimension.</summary>
    private const long DimensionId = 7000;

    /// <summary>v1, published for the first half of 2026.</summary>
    private const long V1 = 701;

    /// <summary>v2, published from July 2026 on, so in effect on the test date.</summary>
    private const long V2 = 702;

    /// <summary>v3, a draft starting in 2027.</summary>
    private const long V3 = 703;

    /// <summary>The test date: 14 October 2026, within v2's window.</summary>
    private static readonly DateTimeOffset TestDate = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>The dimension's three versions.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        DimensionRows.Version(V1, DimensionId, 1, PublicationStatuses.Published, DimensionRows.Utc(2026, 1, 1), DimensionRows.Utc(2026, 7, 1)),
        DimensionRows.Version(V2, DimensionId, 2, PublicationStatuses.Published, DimensionRows.Utc(2026, 7, 1), null),
        DimensionRows.Version(V3, DimensionId, 3, PublicationStatuses.Draft, DimensionRows.Utc(2027, 1, 1), null));

    /// <summary>A <c>region</c> level in each version: 7011 in v1, 7021 in v2 and 7031 in v3.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        DimensionRows.Level(7011, V1, "region", 1),
        DimensionRows.Level(7021, V2, "region", 1),
        DimensionRows.Level(7031, V3, "region", 1));

    /// <summary>The members: NE (7012) in v1; NE (7022) and UNKNOWN (7023) in v2.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(7012, V1, "NE"),
        DimensionRows.Member(7022, V2, "NE"),
        DimensionRows.Member(7023, V2, "UNKNOWN"));

    /// <summary>An unpinned binding whose references are in the version in effect now passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnpinnedReferencesInTheEffectiveVersion_Pass()
    {
        var binding = Binding(pinned: null) with
        {
            GroupingMode = GroupingModes.Level,
            GroupingLevelId = 7021,
            UnmappedBehavior = UnmappedBehaviors.Unknown,
            UnknownMemberId = 7023
        };

        Assert.Empty(await EvaluateAsync(binding));
    }

    /// <summary>
    /// An unpinned binding that still names a level of the earlier published version is reported against the version in
    /// effect now, and a draft version's level is not accepted either.
    /// </summary>
    /// <param name="levelId">The level the binding groups at.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(7011L)]
    [InlineData(7031L)]
    public async Task UnpinnedReferencesOutsideTheEffectiveVersion_AreReported(long levelId)
    {
        var binding = Binding(pinned: null) with { GroupingMode = GroupingModes.Level, GroupingLevelId = levelId };

        var violation = Assert.Single(await EvaluateAsync(binding));

        Assert.Equal(GroupingReferencesInVersionRule.Code, violation.Rule);
        Assert.Equal($"Binding 'Region' groups at level {levelId}, which is not a level of v2, the version of its dimension in effect now.", violation.Message);
    }

    /// <summary>A pinned binding is checked against its pinned version, even when another version is in effect now.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PinnedReferences_AreCheckedAgainstThePinnedVersion()
    {
        var inPinned = Binding(pinned: V1) with { GroupingMode = GroupingModes.Ancestor, GroupingAncestorMemberId = 7012 };
        var inEffective = inPinned with { GroupingAncestorMemberId = 7022 };

        Assert.Empty(await EvaluateAsync(inPinned));
        var violation = Assert.Single(await EvaluateAsync(inEffective));
        Assert.Equal("Binding 'Region' groups under ancestor member 7022, which is not a member of its pinned version v1.", violation.Message);
    }

    /// <summary>An unpinned binding with references, before any version of its dimension is in effect, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnpinnedReferencesWithoutAnEffectiveVersion_AreReported()
    {
        var binding = Binding(pinned: null) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberId = 7023 };

        var violation = Assert.Single(await EvaluateAsync(binding, new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)));

        Assert.Contains("has no published version in effect now", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>Evaluates the rule on a version holding one binding, on the test date.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord binding)
    {
        return EvaluateAsync(binding, TestDate);
    }

    /// <summary>Evaluates the rule on a version holding one binding, at a given time.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord binding, DateTimeOffset now)
    {
        var bindings = new InMemoryTableGateway<DimensionBindingRecord>(binding);
        var rule = new GroupingReferencesInVersionRule(bindings, _versions, _levels, _members, new FakeTimeProvider(now));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>A mapped binding named Region of the dimension, optionally pinned.</summary>
    private static DimensionBindingRecord Binding(long? pinned)
    {
        return DatasetRows.Binding(21, VersionId, "Region", BindingKinds.Mapped, 1) with { DimensionId = DimensionId, PinnedDimensionVersionId = pinned, MappingSetId = 30 };
    }
}

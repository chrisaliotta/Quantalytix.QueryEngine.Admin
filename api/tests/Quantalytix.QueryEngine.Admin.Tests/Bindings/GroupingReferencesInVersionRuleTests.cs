using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>grouping_references_in_version</c> resolves a binding's grouping level, grouping ancestor and unknown
/// member keys in the published version of its dimension, which the active assumption set pins, and ignores retired versions
/// and drafts. A binding with keys whose dimension has no published version is reported too. The engine resolves the keys in
/// that version, so a key it lacks groups or routes nothing.
/// </summary>
public sealed class GroupingReferencesInVersionRuleTests
{
    /// <summary>The dataset version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The bound dimension.</summary>
    private const long DimensionId = 7000;

    /// <summary>v1, retired.</summary>
    private const long V1 = 701;

    /// <summary>v2, published.</summary>
    private const long V2 = 702;

    /// <summary>v3, a draft.</summary>
    private const long V3 = 703;

    /// <summary>The levels: <c>region</c> in every version, <c>district</c> in v1 and <c>zone</c> in v3 only.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        DimensionRows.Level(7011, V1, "region", 1),
        DimensionRows.Level(7012, V1, "district", 2),
        DimensionRows.Level(7021, V2, "region", 1),
        DimensionRows.Level(7031, V3, "region", 1),
        DimensionRows.Level(7032, V3, "zone", 2));

    /// <summary>The members: NE in v1 and v2, UNKNOWN in v2.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(7013, V1, "NE"),
        DimensionRows.Member(7022, V2, "NE"),
        DimensionRows.Member(7023, V2, "UNKNOWN"));

    /// <summary>A binding whose keys the published version has passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysInThePublishedVersion_Pass()
    {
        var binding = Binding() with
        {
            GroupingMode = GroupingModes.Level,
            GroupingLevelKey = "region",
            UnmappedBehavior = UnmappedBehaviors.Unknown,
            UnknownMemberKey = "UNKNOWN"
        };

        Assert.Empty(await EvaluateAsync(binding, Published()));
    }

    /// <summary>A level that only a retired version or a draft has is reported against the published version.</summary>
    /// <param name="levelKey">The level the binding groups at.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData("district")]
    [InlineData("zone")]
    public async Task KeysOutsideThePublishedVersion_AreReported(string levelKey)
    {
        var binding = Binding() with { GroupingMode = GroupingModes.Level, GroupingLevelKey = levelKey };

        var violation = Assert.Single(await EvaluateAsync(binding, Published()));

        Assert.Equal(GroupingReferencesInVersionRule.Code, violation.Rule);
        Assert.Equal($"Binding 'Region' groups at level '{levelKey}', which is not a level of v2, the published version of its dimension.", violation.Message);
    }

    /// <summary>A binding with keys whose dimension has no published version is reported once.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysWithoutAPublishedVersion_AreReported()
    {
        var binding = Binding() with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberKey = "UNKNOWN" };
        var unpublished = new InMemoryTableGateway<DimensionVersionRecord>(DimensionRows.Version(V3, DimensionId, 3, PublicationStatuses.Draft));

        var violation = Assert.Single(await EvaluateAsync(binding, unpublished));

        Assert.Equal("Binding 'Region' names levels or members of its dimension, which has no published version.", violation.Message);
    }

    /// <summary>The dimension's versions: v1 retired, v2 published and v3 a draft.</summary>
    private static InMemoryTableGateway<DimensionVersionRecord> Published()
    {
        return new InMemoryTableGateway<DimensionVersionRecord>(
            DimensionRows.Version(V1, DimensionId, 1, PublicationStatuses.Retired),
            DimensionRows.Version(V2, DimensionId, 2, PublicationStatuses.Published),
            DimensionRows.Version(V3, DimensionId, 3, PublicationStatuses.Draft));
    }

    /// <summary>Evaluates the rule on a version holding one binding, over the given dimension versions.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord binding, InMemoryTableGateway<DimensionVersionRecord> versions)
    {
        var rule = new GroupingReferencesInVersionRule(new InMemoryTableGateway<DimensionBindingRecord>(binding), new PublishedDimensionReader(versions, _levels, _members));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>A mapped binding named Region of the dimension.</summary>
    private static DimensionBindingRecord Binding()
    {
        return DatasetRows.Binding(21, VersionId, "Region", BindingKinds.Mapped, 1) with { DimensionId = DimensionId, MappingSetId = 30 };
    }
}

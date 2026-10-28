using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Bridges.BridgeRows;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the bridge mapping rules that depend on the version's settings, over a snapshot: <c>allocation_partition</c> requires each
/// source member's weights to sum to exactly 1 in decimal arithmetic (0.086 + 0.35 + 0.564 passes, 0.3 + 0.6 fails) and every weight to
/// be 0 or more; <c>allocation_none</c> allows one target per source with a weight of exactly 1; <c>cardinality_many_to_one</c> allows
/// one target per source. The engine multiplies fact values by these weights, so a partition that does not sum to 1 loses or invents
/// value, and a second target in a one-target mode counts a source twice. <c>mapping_members_in_endpoint_versions</c> resolves the
/// mappings' member keys in the live versions of the bridge's dimensions, which the active assumption set pins; <c>pair_unique</c> and
/// <c>descendants_report_only</c> keep a pair once and descendants to report bridges (B6, B8).
/// </summary>
public sealed class BridgeMappingRuleTests
{
    /// <summary>The bridge version being checked.</summary>
    private const long VersionId = 50;

    /// <summary>Weights summing to exactly 1 pass, including 0.086 + 0.35 + 0.564, which binary floating point sums to 0.9999999999999999.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionWeightsSummingToOne_Pass()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Partition,
                Mapping(VersionId, "LOAN_A", "EAST", 0.086m),
                Mapping(VersionId, "LOAN_A", "WEST", 0.35m),
                Mapping(VersionId, "LOAN_A", "NORTH", 0.564m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m)));

        Assert.Empty(violations);
    }

    /// <summary>Weights of 0.3 and 0.6 leave a tenth unallocated, which the violation names on the source member with the sum.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionWeightsSummingToLessThanOne_Fail()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Partition,
                Mapping(VersionId, "LOAN_A", "EAST", 0.3m),
                Mapping(VersionId, "LOAN_A", "WEST", 0.6m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m)));

        var violation = Assert.Single(violations);
        Assert.Equal("allocation_partition", violation.Code);
        Assert.Equal("Source member 'LOAN_A' has allocation weights summing to 0.9; partition requires exactly 1.", violation.Message);
        Assert.Equal("LOAN_A", violation.Target!.ItemKey);
    }

    /// <summary>A negative weight fails even when the source's weights sum to 1, on the mapping that has it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task NegativePartitionWeight_Fails()
    {
        var negative = Mapping(VersionId, "LOAN_A", "WEST", -0.5m);
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Partition, Mapping(VersionId, "LOAN_A", "EAST", 1.5m), negative));

        var violation = Assert.Single(violations);
        Assert.Equal("Mapping 'LOAN_A' -> 'WEST' has allocation weight -0.5; weights must be 0 or more.", violation.Message);
        Assert.Equal(TemporaryIds.PresentStored(negative.BridgeMappingId), violation.Target!.ItemId);
    }

    /// <summary>Partition weights are not checked in another allocation mode.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionRule_SkipsOtherModes()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Replicate, Mapping(VersionId, "LOAN_A", "EAST", 1m), Mapping(VersionId, "LOAN_A", "WEST", 1m)));

        Assert.Empty(violations);
    }

    /// <summary>With allocation mode none, one target per source with a weight of 1 passes, whatever the factor.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AllocationNone_OneTargetWeightedOne_Passes()
    {
        var violations = await EvaluateAsync(
            new AllocationNoneRule(),
            Snapshot(Cardinalities.ManyToOne, AllocationModes.None, Mapping(VersionId, "LOAN_A", "EAST", 1m), Mapping(VersionId, "LOAN_B", "EAST", 1m, factor: -1m)));

        Assert.Empty(violations);
    }

    /// <summary>
    /// With allocation mode none on a stored <c>many_to_many</c> version (B5 refuses the pair as input), a source's second target and any
    /// weight other than 1 fail.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AllocationNone_SecondTargetAndOtherWeights_Fail()
    {
        var violations = await EvaluateAsync(
            new AllocationNoneRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.None,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m),
                Mapping(VersionId, "LOAN_B", "WEST", 0.5m)));

        Assert.All(violations, violation => Assert.Equal("allocation_none", violation.Code));
        Assert.Equal(
            [
                "Source member 'LOAN_A' maps to 2 targets ('EAST', 'WEST'); allocation mode none allows one.",
                "Mapping 'LOAN_B' -> 'WEST' has allocation weight 0.5; allocation mode none requires exactly 1."
            ],
            violations.Select(violation => violation.Message));
    }

    /// <summary>Under <c>many_to_one</c>, a second target is <c>cardinality_many_to_one</c>'s finding only, so the source is reported once.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ManyToOneNone_SecondTarget_IsReportedOnce()
    {
        var snapshot = Snapshot(Cardinalities.ManyToOne, AllocationModes.None, Mapping(VersionId, "LOAN_A", "EAST", 1m), Mapping(VersionId, "LOAN_A", "WEST", 1m));

        var violations = await MappingsFixture.BridgeRules().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(["cardinality_many_to_one"], violations.Select(violation => violation.Code));
    }

    /// <summary>With cardinality many_to_one, a source with two targets fails, whatever the allocation mode.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ManyToOne_SecondTarget_Fails()
    {
        var violations = await EvaluateAsync(
            new CardinalityManyToOneRule(),
            Snapshot(Cardinalities.ManyToOne, AllocationModes.Replicate,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m)));

        var violation = Assert.Single(violations);
        Assert.Equal("cardinality_many_to_one", violation.Code);
        Assert.Equal("Source member 'LOAN_A' maps to 2 targets ('EAST', 'WEST'); cardinality many_to_one allows one.", violation.Message);
    }

    /// <summary>With cardinality many_to_many, a source may have several targets.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ManyToMany_SeveralTargets_Pass()
    {
        var violations = await EvaluateAsync(
            new CardinalityManyToOneRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Replicate, Mapping(VersionId, "LOAN_A", "EAST", 1m), Mapping(VersionId, "LOAN_A", "WEST", 1m)));

        Assert.Empty(violations);
    }

    /// <summary>A target key the target dimension's live version lacks is reported; the live keys pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TargetMissingFromTheLiveVersion_IsReported()
    {
        var violations = await EvaluateAsync(
            new MappingMembersInEndpointVersionsRule(),
            Snapshot(Cardinalities.ManyToOne, AllocationModes.None, Mapping(VersionId, "LOAN_A", "EAST", 1m), Mapping(VersionId, "LOAN_B", "SOUTH", 1m)));

        var violation = Assert.Single(violations);
        Assert.Equal("mapping_members_in_endpoint_versions", violation.Code);
        Assert.Equal("Mapping 'LOAN_B' -> 'SOUTH' has a target member that the live target dimension version lacks.", violation.Message);
    }

    /// <summary>B6: a pair that appears twice is reported on its second mapping.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedPair_IsReported()
    {
        var repeated = Mapping(VersionId, "LOAN_A", "EAST", 1m);
        var violations = await EvaluateAsync(
            new PairUniqueRule(),
            Snapshot(Cardinalities.ManyToMany, AllocationModes.Replicate, Mapping(VersionId, "LOAN_A", "EAST", 1m), repeated));

        var violation = Assert.Single(violations);
        Assert.Equal("pair_unique", violation.Code);
        Assert.Equal(TemporaryIds.PresentStored(repeated.BridgeMappingId), violation.Target!.ItemId);
    }

    /// <summary>B8: a mapping of a member with its descendants is reported on an analytical bridge and allowed on a report bridge.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task Descendants_AreReportedOnlyOnAnalyticalBridges()
    {
        var snapshot = Snapshot(Cardinalities.ManyToMany, AllocationModes.Replicate, Mapping(VersionId, "LOAN_A", "EAST", 1m) with { SourceSelection = SourceSelections.Descendants });
        var reportBridge = snapshot with { Surroundings = snapshot.Surroundings with { Target = snapshot.Surroundings.Target with { DimensionPurpose = DimensionPurposes.Report } } };

        Assert.Equal(["descendants_report_only"], (await EvaluateAsync(new DescendantsReportOnlyRule(), snapshot)).Select(violation => violation.Code));
        Assert.Empty(await EvaluateAsync(new DescendantsReportOnlyRule(), reportBridge));
    }

    /// <summary>Runs a rule over a snapshot.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(ISnapshotRule<BridgeSnapshot> rule, BridgeSnapshot snapshot)
    {
        return rule.EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The live version of analytical bridge 1 from dimension 1 (live members LOAN_A, LOAN_B) to dimension 2 (live members EAST, WEST,
    /// NORTH), with the given settings and mappings.
    /// </summary>
    private static BridgeSnapshot Snapshot(string cardinality, string allocationMode, params BridgeMappingRecord[] mappings)
    {
        var version = BridgeVersion(VersionId, 1, 1, cardinality, allocationMode, PublicationStatuses.Published);
        return new BridgeSnapshot
        {
            Bridge = Bridge(1, 1, 2),
            Version = version,
            Mappings = mappings,
            Surroundings = new BridgeSurroundings
            {
                Source = Dimension(1, DimensionKinds.Categorical),
                SourcePublished = Keys(Version(10, 1, 1, PublicationStatuses.Published), "LOAN_A", "LOAN_B"),
                Target = Dimension(2, DimensionKinds.Categorical),
                TargetPublished = Keys(Version(20, 2, 1, PublicationStatuses.Published), "EAST", "WEST", "NORTH"),
                Owner = null,
                Versions = [version],
                MappingCounts = new Dictionary<long, int> { [VersionId] = mappings.Length },
                Bindings = [],
                Pins = []
            }
        };
    }

    /// <summary>The keys of a flat dimension version with the given members.</summary>
    private static DimensionKeys Keys(DimensionVersionRecord version, params string[] memberKeys)
    {
        return new DimensionKeys
        {
            Version = version,
            LevelKeys = new HashSet<string>(),
            MemberKeys = memberKeys.ToHashSet(StringComparer.Ordinal),
            UnassignableMemberKeys = new HashSet<string>(),
            RootCount = memberKeys.Length,
            ParentKeys = new Dictionary<string, string>()
        };
    }
}

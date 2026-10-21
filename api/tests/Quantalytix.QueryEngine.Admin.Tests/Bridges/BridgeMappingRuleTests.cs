using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Bridges.BridgeRows;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the bridge mapping rules that depend on the version's semantics: <c>allocation_partition</c> requires each
/// source member's weights to sum to exactly 1 in decimal arithmetic (0.086 + 0.35 + 0.564 passes, 0.3 + 0.6 fails) and every
/// weight to be 0 or more; <c>allocation_none</c> allows one target per source with a weight of exactly 1;
/// <c>cardinality_many_to_one</c> allows one target per source. The engine multiplies fact values by these weights, so a
/// partition that does not sum to 1 loses or invents value, and a second target in a one-target mode counts a source twice.
/// <c>mapping_members_in_endpoint_versions</c> resolves the mappings' member keys in the published versions of the bridge's
/// dimensions, which the active assumption set pins.
/// </summary>
public sealed class BridgeMappingRuleTests
{
    /// <summary>The bridge version being validated.</summary>
    private const long VersionId = 50;

    /// <summary>The published version of the bridge's source dimension 1.</summary>
    private const long SourceVersionId = 10;

    /// <summary>The published version of the bridge's target dimension 2.</summary>
    private const long TargetVersionId = 20;

    /// <summary>The members: LOAN_A and LOAN_B in the source version; EAST, WEST and NORTH in the target version; SOUTH only in a target draft.</summary>
    private static readonly DimensionMemberRecord[] Members =
    [
        Member(1, SourceVersionId, "LOAN_A"),
        Member(2, SourceVersionId, "LOAN_B"),
        Member(11, TargetVersionId, "EAST"),
        Member(12, TargetVersionId, "WEST"),
        Member(13, TargetVersionId, "NORTH"),
        Member(14, 21, "SOUTH")
    ];

    /// <summary>
    /// Weights summing to exactly 1 pass, including 0.086 + 0.35 + 0.564, which binary floating point sums to
    /// 0.9999999999999999 in every order.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionWeightsSummingToOne_Pass()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(Reader(Cardinalities.ManyToMany, AllocationModes.Partition,
                Mapping(VersionId, "LOAN_A", "EAST", 0.086m),
                Mapping(VersionId, "LOAN_A", "WEST", 0.35m),
                Mapping(VersionId, "LOAN_A", "NORTH", 0.564m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m))));

        Assert.Empty(violations);
    }

    /// <summary>Weights of 0.3 and 0.6 leave a tenth unallocated, which the violation names with the source member and the sum.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionWeightsSummingToLessThanOne_Fail()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(Reader(Cardinalities.ManyToMany, AllocationModes.Partition,
                Mapping(VersionId, "LOAN_A", "EAST", 0.3m),
                Mapping(VersionId, "LOAN_A", "WEST", 0.6m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m))));

        var violation = Assert.Single(violations);
        Assert.Equal(AllocationPartitionRule.Code, violation.Rule);
        Assert.Equal("Source member 'LOAN_A' has allocation weights summing to 0.9; partition requires exactly 1.", violation.Message);
    }

    /// <summary>A negative weight fails even when the source's weights sum to 1.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task NegativePartitionWeight_Fails()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(Reader(Cardinalities.ManyToMany, AllocationModes.Partition,
                Mapping(VersionId, "LOAN_A", "EAST", 1.5m),
                Mapping(VersionId, "LOAN_A", "WEST", -0.5m))));

        Assert.Equal(["Mapping 'LOAN_A' -> 'WEST' has allocation weight -0.5; weights must be 0 or more."], violations.Select(violation => violation.Message));
    }

    /// <summary>Partition weights are not checked in another allocation mode.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PartitionRule_SkipsOtherModes()
    {
        var violations = await EvaluateAsync(
            new AllocationPartitionRule(Reader(Cardinalities.ManyToMany, AllocationModes.Replicate,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m))));

        Assert.Empty(violations);
    }

    /// <summary>With allocation mode none, one target per source with a weight of 1 passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AllocationNone_OneTargetWeightedOne_Passes()
    {
        var violations = await EvaluateAsync(
            new AllocationNoneRule(Reader(Cardinalities.ManyToOne, AllocationModes.None,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_B", "EAST", 1m, factor: -1m))));

        Assert.Empty(violations);
    }

    /// <summary>With allocation mode none, a source's second target and any weight other than 1 fail.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AllocationNone_SecondTargetAndOtherWeights_Fail()
    {
        var violations = await EvaluateAsync(
            new AllocationNoneRule(Reader(Cardinalities.ManyToMany, AllocationModes.None,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m),
                Mapping(VersionId, "LOAN_B", "WEST", 0.5m))));

        Assert.All(violations, violation => Assert.Equal(AllocationNoneRule.Code, violation.Rule));
        Assert.Equal(
            [
                "Source member 'LOAN_A' maps to 2 targets ('EAST', 'WEST'); allocation mode none allows one.",
                "Mapping 'LOAN_B' -> 'WEST' has allocation weight 0.5; allocation mode none requires exactly 1."
            ],
            violations.Select(violation => violation.Message));
    }

    /// <summary>With cardinality many_to_one, a source with two targets fails, whatever the allocation mode.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ManyToOne_SecondTarget_Fails()
    {
        var violations = await EvaluateAsync(
            new CardinalityManyToOneRule(Reader(Cardinalities.ManyToOne, AllocationModes.Replicate,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m),
                Mapping(VersionId, "LOAN_B", "WEST", 1m))));

        var violation = Assert.Single(violations);
        Assert.Equal(CardinalityManyToOneRule.Code, violation.Rule);
        Assert.Equal("Source member 'LOAN_A' maps to 2 targets ('EAST', 'WEST'); cardinality many_to_one allows one.", violation.Message);
    }

    /// <summary>With cardinality many_to_many, a source may have several targets.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ManyToMany_SeveralTargets_Pass()
    {
        var violations = await EvaluateAsync(
            new CardinalityManyToOneRule(Reader(Cardinalities.ManyToMany, AllocationModes.Replicate,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_A", "WEST", 1m))));

        Assert.Empty(violations);
    }

    /// <summary>A target key that only a draft of the target dimension has is reported; the published keys pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TargetMissingFromThePublishedVersion_IsReported()
    {
        var violations = await EvaluateAsync(
            new MappingMembersInEndpointVersionsRule(Reader(Cardinalities.ManyToOne, AllocationModes.None,
                Mapping(VersionId, "LOAN_A", "EAST", 1m),
                Mapping(VersionId, "LOAN_B", "SOUTH", 1m))));

        var violation = Assert.Single(violations);
        Assert.Equal(MappingMembersInEndpointVersionsRule.Code, violation.Rule);
        Assert.Equal("Mapping 'LOAN_B' -> 'SOUTH' has a target member that the published target dimension version lacks.", violation.Message);
    }

    /// <summary>Runs a rule over the bridge version.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IPublicationRule rule)
    {
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A mapping set reader over a draft version of bridge 1, from dimension 1 to dimension 2, with the given semantics and
    /// mappings, and the members of the dimensions' versions.
    /// </summary>
    private static BridgeMappingSetReader Reader(string cardinality, string allocationMode, params BridgeMappingRecord[] mappings)
    {
        var dimensions = new PublishedDimensionReader(
            new InMemoryTableGateway<DimensionVersionRecord>(
                Version(SourceVersionId, 1, 1, PublicationStatuses.Published),
                Version(TargetVersionId, 2, 1, PublicationStatuses.Published),
                Version(21, 2, 2, PublicationStatuses.Draft)),
            new InMemoryTableGateway<DimensionLevelRecord>(),
            new InMemoryTableGateway<DimensionMemberRecord>(Members),
            new PublicationSet());
        return new BridgeMappingSetReader(
            new InMemoryTableGateway<BridgeVersionRecord>(BridgeVersion(VersionId, 1, 1, cardinality, allocationMode, PublicationStatuses.Draft)),
            new InMemoryTableGateway<BridgeRecord>(Bridge(1, 1, 2)),
            new InMemoryTableGateway<BridgeMappingRecord>(mappings),
            dimensions);
    }
}

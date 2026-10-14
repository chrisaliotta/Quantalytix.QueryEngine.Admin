using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Bridges.BridgeRows;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms that creating a bridge version as a clone moves the source version's mappings onto new endpoint versions by
/// member key: a mapping whose source key exists in the new source version and whose target key exists in the new target
/// version keeps its factor and weight between those members, and any other mapping is dropped and reported by its keys.
/// Member IDs differ between dimension versions, so a clone that kept them would remap facts between the old versions' members.
/// </summary>
public sealed class BridgeVersionClonerTests
{
    /// <summary>The bridge being versioned.</summary>
    private const long BridgeId = 1;

    /// <summary>The published bridge version that is cloned.</summary>
    private const long SourceBridgeVersionId = 50;

    /// <summary>The source dimension version the clone pins.</summary>
    private const long NewSourceVersionId = 11;

    /// <summary>The target dimension version the clone pins.</summary>
    private const long NewTargetVersionId = 21;

    /// <summary>
    /// The members: the old source version 10 has LOAN_A, LOAN_B and LOAN_C, the old target version 20 has EAST and WEST;
    /// the new source version has LOAN_A and LOAN_B, the new target version EAST and SOUTH, all under new IDs.
    /// </summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        Member(100, 10, "LOAN_A"),
        Member(101, 10, "LOAN_B"),
        Member(102, 10, "LOAN_C"),
        Member(200, 20, "EAST"),
        Member(201, 20, "WEST"),
        Member(110, NewSourceVersionId, "LOAN_A"),
        Member(111, NewSourceVersionId, "LOAN_B"),
        Member(210, NewTargetVersionId, "EAST"),
        Member(212, NewTargetVersionId, "SOUTH"));

    /// <summary>The bridge versions: the cloned published v1.</summary>
    private readonly InMemoryTableGateway<BridgeVersionRecord> _versions = new(
        BridgeVersion(SourceBridgeVersionId, BridgeId, 1, 10, 20, Cardinalities.ManyToMany, AllocationModes.Partition, PublicationStatuses.Published));

    /// <summary>The mappings of v1: LOAN_A split 0.4/0.6 over EAST and WEST, LOAN_B onto EAST with factor -1, and LOAN_C onto EAST.</summary>
    private readonly InMemoryTableGateway<BridgeMappingRecord> _mappings = new(
        Mapping(SourceBridgeVersionId, 100, 200, 0.4m),
        Mapping(SourceBridgeVersionId, 100, 201, 0.6m),
        Mapping(SourceBridgeVersionId, 101, 200, 1m, factor: -1m),
        Mapping(SourceBridgeVersionId, 102, 200, 1m));

    /// <summary>The clone is draft v2 on the new endpoints, and LOAN_A -> EAST and LOAN_B -> EAST move to the new members with their factor and weight.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_RemapsMappingsByMemberKeys()
    {
        var created = await CloneAsync();

        Assert.Equal(
            BridgeVersion(created.Version.BridgeVersionId, BridgeId, 2, NewSourceVersionId, NewTargetVersionId, Cardinalities.ManyToMany, AllocationModes.Partition, PublicationStatuses.Draft),
            created.Version);
        Assert.NotEqual(SourceBridgeVersionId, created.Version.BridgeVersionId);
        Assert.Equal(
            [Mapping(created.Version.BridgeVersionId, 110, 210, 0.4m), Mapping(created.Version.BridgeVersionId, 111, 210, 1m, factor: -1m)],
            _mappings.All.Where(mapping => mapping.BridgeVersionId == created.Version.BridgeVersionId).OrderBy(mapping => mapping.SourceMemberId));
        Assert.Equal(4, _mappings.All.Count(mapping => mapping.BridgeVersionId == SourceBridgeVersionId));
    }

    /// <summary>LOAN_A -> WEST loses its target and LOAN_C -> EAST its source, so both are dropped and reported by source key, then target key.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_ReportsTheMappingsItDropped()
    {
        var created = await CloneAsync();

        Assert.Equal(
            [
                new DroppedBridgeMapping { SourceMemberKey = "LOAN_A", TargetMemberKey = "WEST" },
                new DroppedBridgeMapping { SourceMemberKey = "LOAN_C", TargetMemberKey = "EAST" }
            ],
            created.DroppedMappings);
    }

    /// <summary>Creates the bridge's next version on the new endpoints as a clone of v1, with this test's tables.</summary>
    private Task<CreatedBridgeVersion> CloneAsync()
    {
        var ids = new SequentialIdGenerator(1000);
        var service = new BridgeVersionService(
            new InMemoryTableGateway<BridgeRecord>(Bridge(BridgeId, 1, 2)),
            _versions,
            _mappings,
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new BridgeVersionCloner(_mappings, _members),
            ids);
        var input = new NewBridgeVersionInput
        {
            SourceDimensionVersionId = NewSourceVersionId,
            TargetDimensionVersionId = NewTargetVersionId,
            Cardinality = Cardinalities.ManyToMany,
            AllocationMode = AllocationModes.Partition,
            CloneFromVersionId = SourceBridgeVersionId
        };
        return service.CreateAsync(BridgeId, input, TestContext.Current.CancellationToken);
    }
}

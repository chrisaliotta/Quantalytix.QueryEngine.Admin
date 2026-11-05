using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// In-memory tables of one tenant and the mappings module's loaders, rules, change-set pipelines and commands over them, so tests run
/// the module as the API does. Seeds a hierarchical branch dimension (US, a folder over CA and NY, in live version 10; TX only in frozen
/// version 11), a categorical region dimension (EAST, WEST, NORTH), report 60's dimension (LOANS), a time dimension; mapping set 300
/// onto branches with live version 40 and version 41 in the frozen snapshot; an analytical bridge 50 from branches to regions; and
/// source <c>gl</c>'s report bridge 70 from branches into the report's rows.
/// </summary>
internal sealed class MappingsFixture
{
    /// <summary>The hierarchical branch dimension.</summary>
    public const long BranchDimensionId = 1;

    /// <summary>The categorical region dimension.</summary>
    public const long RegionDimensionId = 2;

    /// <summary>A time dimension, which nothing bridges.</summary>
    public const long TimeDimensionId = 4;

    /// <summary>The mapping set onto branches.</summary>
    public const long MappingSetId = 300;

    /// <summary>Its live version.</summary>
    public const long LiveMappingVersionId = 40;

    /// <summary>Its version in the frozen snapshot.</summary>
    public const long FrozenMappingVersionId = 41;

    /// <summary>The analytical bridge from branches to regions.</summary>
    public const long BridgeId = 50;

    /// <summary>Its live version, <c>many_to_one</c> / <c>none</c>.</summary>
    public const long LiveBridgeVersionId = 51;

    /// <summary>Report source <c>gl</c>'s bridge from branches into report 60's rows.</summary>
    public const long ReportBridgeId = 70;

    /// <summary>Its live version, <c>many_to_many</c> / <c>replicate</c>.</summary>
    public const long ReportBridgeVersionId = 71;

    /// <summary>The first id the generator allocates.</summary>
    public const long FirstNewId = 900;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>The clock every service reads.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The id generator every service shares, so their ids never collide.</summary>
    private readonly SequentialIdGenerator _ids = new(FirstNewId);

    /// <summary>The dimension table.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(BranchDimensionId, DimensionKinds.Hierarchical) with { DimensionKey = "branch" },
        Dimension(RegionDimensionId, DimensionKinds.Categorical) with { DimensionKey = "region" },
        Dimension(ReportRows.ReportDimensionId, DimensionKinds.Hierarchical) with { DimensionKey = "balance_sheet_rows", DimensionPurpose = DimensionPurposes.Report },
        Dimension(TimeDimensionId, DimensionKinds.Time) with { DimensionKey = "business_date" });

    /// <summary>The dimension version table.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        Version(10, BranchDimensionId),
        Version(11, BranchDimensionId, SnapshotRows.FrozenId),
        Version(20, RegionDimensionId),
        Version(30, ReportRows.ReportDimensionId));

    /// <summary>The member table.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        Member(1, 10, "US") with { IsAssignable = false },
        Member(2, 10, "CA", parentId: 1),
        Member(3, 10, "NY", parentId: 1),
        Member(9, 11, "TX"),
        Member(11, 20, "EAST"),
        Member(12, 20, "WEST"),
        Member(13, 20, "NORTH"),
        Member(21, 30, "LOANS"));

    /// <summary>The mapping set table.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new(MappingRows.MappingSet(MappingSetId, BranchDimensionId));

    /// <summary>The mapping version table.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new(
        MappingRows.MappingVersion(LiveMappingVersionId, MappingSetId),
        MappingRows.MappingVersion(FrozenMappingVersionId, MappingSetId, SnapshotRows.FrozenId));

    /// <summary>The mapping entry table: '06' maps to CA in both versions.</summary>
    public InMemoryTableGateway<DimensionMappingRecord> Entries { get; } = new(
        MappingRows.Entry(400, LiveMappingVersionId, "06", "CA"),
        MappingRows.Entry(401, FrozenMappingVersionId, "06", "CA"));

    /// <summary>The bridge table.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(
        BridgeRows.Bridge(BridgeId, BranchDimensionId, RegionDimensionId),
        BridgeRows.Bridge(ReportBridgeId, BranchDimensionId, ReportRows.ReportDimensionId) with { BridgeKey = "balance_sheet.gl" });

    /// <summary>The bridge version table.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(
        BridgeRows.BridgeVersion(LiveBridgeVersionId, BridgeId, Cardinalities.ManyToOne, AllocationModes.None),
        BridgeRows.BridgeVersion(ReportBridgeVersionId, ReportBridgeId, Cardinalities.ManyToMany, AllocationModes.Replicate));

    /// <summary>The bridge mapping table: CA to WEST in the analytical bridge, US with its descendants into LOANS in the report bridge.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new(
        BridgeRows.Mapping(LiveBridgeVersionId, "CA", "WEST", 1m),
        BridgeRows.Mapping(ReportBridgeVersionId, "US", "LOANS", 1m) with { SourceSelection = SourceSelections.Descendants });

    /// <summary>The binding table.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new();

    /// <summary>The dataset version table: live version 1000 of dataset 10.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(DatasetRows.Version(1000));

    /// <summary>The dataset table.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(new DatasetRecord { DatasetId = 10, DatasetKey = "bank.loans", DisplayName = "Loans" });

    /// <summary>The role table: the branch dimension's role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(Role(81, "branch", BranchDimensionId), Role(82, "region", RegionDimensionId));

    /// <summary>The field table: version 1000's branch code.</summary>
    public InMemoryTableGateway<FieldRecord> Fields { get; } = new(DatasetRows.ColumnField(90, 1000, "branch_code", "branch_code"));

    /// <summary>The report table: report 60.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(ReportRows.Report());

    /// <summary>The report version table: report 60's live version.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(ReportRows.Version());

    /// <summary>The report source table: bridge source <c>gl</c>, which owns bridge 70.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(
        ReportRows.Source(80, "gl", 10) with { MappingMethod = ReportMappingMethods.Bridge, FieldKey = null, DimensionRoleId = 81, BridgeId = ReportBridgeId });

    /// <summary>The snapshot table: the live snapshot and one frozen snapshot.</summary>
    public InMemoryTableGateway<SnapshotRecord> Snapshots { get; } = new(SnapshotRows.Live(), SnapshotRows.Frozen());

    /// <summary>The tenant's database info row.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 20,
        SemanticRevision = StartingRevision,
        UpdatedAt = DateTime.UnixEpoch
    });

    /// <summary>The tenant's semantic revision.</summary>
    public long Revision => DatabaseInfo.All.Single().SemanticRevision;

    /// <summary>Binds mapping set 300 and bridge 50 in dataset version 1000, the bridge remapping the mapped branch binding to regions.</summary>
    /// <returns>A task that completes when the bindings are stored.</returns>
    public Task BindAsync()
    {
        return Bindings.InsertManyAsync(
            [
                DatasetRows.Binding(21, 1000, 81, BindingKinds.Mapped, 90) with { DimensionId = BranchDimensionId, MappingSetId = MappingSetId },
                DatasetRows.Binding(22, 1000, 82, BindingKinds.Bridge, null) with { DimensionId = RegionDimensionId, SourceDimensionBindingId = 21, BridgeId = BridgeId }
            ],
            CancellationToken.None);
    }

    /// <summary>The mapping set snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public MappingSetSnapshotLoader MappingLoader()
    {
        return new MappingSetSnapshotLoader(MappingSets, MappingVersions, Entries, Dimensions, DimensionKeys(), References(), Live(), Holders());
    }

    /// <summary>Every mapping rule, in the API's registration order.</summary>
    /// <returns>The rule runner.</returns>
    public static SnapshotRuleRunner<MappingSetSnapshot> MappingRules()
    {
        return new SnapshotRuleRunner<MappingSetSnapshot>(MappingRuleList());
    }

    /// <summary>The mapping set change set, as the API registers it.</summary>
    /// <returns>The change set.</returns>
    public MappingSetChanges MappingChanges()
    {
        var aggregate = new MappingSetAggregate(MappingLoader(), MappingRules(), MappingSets, Entries);
        return new MappingSetChanges(new ChangeSetPipeline<MappingSetSnapshot, MappingOperation, MappingSetEditState>(
            aggregate, MappingRuleList(), [new MappingOperationValidator()], _ids, SemanticRevision()));
    }

    /// <summary>The mapping set reads.</summary>
    /// <returns>The queries.</returns>
    public MappingQueries MappingQueries()
    {
        return new MappingQueries(MappingSets, MappingVersions, Entries, References(), MappingLoader(), MappingRules(), Live());
    }

    /// <summary>The mapping set create and delete commands.</summary>
    /// <returns>The commands.</returns>
    public MappingCommands MappingCommands()
    {
        return new MappingCommands(MappingSets, MappingVersions, Dimensions, MappingRemover(), MappingLoader(), Live(), SemanticRevision(), _ids, _time);
    }

    /// <summary>The mapping set copier capture and restore use.</summary>
    /// <returns>The copier.</returns>
    public MappingVersionCopier MappingCopier()
    {
        return new MappingVersionCopier(MappingVersions, Entries, _ids, _time);
    }

    /// <summary>The mapping set remover snapshot deletes and restores use.</summary>
    /// <returns>The remover.</returns>
    public MappingVersionRemover MappingRemover()
    {
        return new MappingVersionRemover(MappingSets, MappingVersions, Entries);
    }

    /// <summary>The bridge snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public BridgeSnapshotLoader BridgeLoader()
    {
        return new BridgeSnapshotLoader(Bridges, BridgeVersions, BridgeMappings, Dimensions, DimensionKeys(), References(), Owners(), Live(), Holders());
    }

    /// <summary>Every bridge rule, in the API's registration order.</summary>
    /// <returns>The rule runner.</returns>
    public static SnapshotRuleRunner<BridgeSnapshot> BridgeRules()
    {
        return new SnapshotRuleRunner<BridgeSnapshot>(BridgeRuleList());
    }

    /// <summary>The analytical bridge change set, as the API registers it.</summary>
    /// <returns>The change set.</returns>
    public BridgeChanges BridgeChanges()
    {
        var aggregate = new BridgeAggregate(BridgeLoader(), BridgeRules(), Bridges, BridgeVersions, BridgeMappings);
        var pipeline = new ChangeSetPipeline<BridgeSnapshot, BridgeOperation, BridgeEditState>(
            aggregate, BridgeRuleList(), [new BridgeOperationValidator()], _ids, SemanticRevision());
        return new BridgeChanges(pipeline, Owners(), Bridges, Live());
    }

    /// <summary>The bridge reads.</summary>
    /// <returns>The queries.</returns>
    public BridgeQueries BridgeQueries()
    {
        return new BridgeQueries(Bridges, BridgeVersions, BridgeMappings, Sources, ReportVersions, References(), Owners(), BridgeLoader(), BridgeRules(), Live());
    }

    /// <summary>The analytical bridge create and delete commands.</summary>
    /// <returns>The commands.</returns>
    public BridgeCommands BridgeCommands()
    {
        return new BridgeCommands(Bridges, Dimensions, BridgeStore(), BridgeRemover(), BridgeLoader(), SemanticRevision(), _ids);
    }

    /// <summary>The bridge copier capture and restore use.</summary>
    /// <returns>The copier.</returns>
    public BridgeVersionCopier BridgeCopier()
    {
        return new BridgeVersionCopier(BridgeVersions, BridgeMappings, BridgeStore(), _ids, _time);
    }

    /// <summary>The bridge remover snapshot deletes and restores use.</summary>
    /// <returns>The remover.</returns>
    public BridgeVersionRemover BridgeRemover()
    {
        return new BridgeVersionRemover(Bridges, BridgeVersions, BridgeMappings);
    }

    /// <summary>The live snapshot over the snapshot table.</summary>
    /// <returns>The reader.</returns>
    public LiveSnapshot Live()
    {
        return new LiveSnapshot(Snapshots);
    }

    /// <summary>The reports module's hook that writes report bridges.</summary>
    /// <returns>The hook.</returns>
    public ReportBridges ReportBridges()
    {
        return new ReportBridges(Bridges, BridgeVersions, BridgeMappings, BridgeStore(), BridgeRemover(), Live(), _ids);
    }

    /// <summary>Every mapping rule.</summary>
    private static ISnapshotRule<MappingSetSnapshot>[] MappingRuleList()
    {
        return [new DimensionVersionInSnapshotRule(), new SourceKeyUniqueRule(), new EntryMemberInVersionRule(), new EntryMemberAssignableRule()];
    }

    /// <summary>Every bridge rule.</summary>
    private static ISnapshotRule<BridgeSnapshot>[] BridgeRuleList()
    {
        return
        [
            new EndpointVersionsInSnapshotRule(), new MappingMembersInEndpointVersionsRule(), new PairUniqueRule(), new CardinalityManyToOneRule(),
            new AllocationNoneRule(), new AllocationPartitionRule(), new AllocationReplicateRule(), new DescendantsReportOnlyRule()
        ];
    }

    /// <summary>The readers both kinds share.</summary>
    private MappingReferenceReader References()
    {
        return new MappingReferenceReader(Dimensions, DimensionVersions, Bindings, DatasetVersions, Datasets, Roles, Fields);
    }

    /// <summary>The reader of dimensions' versions in a snapshot.</summary>
    private SnapshotDimensionReader DimensionKeys()
    {
        return new SnapshotDimensionReader(DimensionVersions, new InMemoryTableGateway<DimensionLevelRecord>(), Members);
    }

    /// <summary>The reader of the frozen snapshots that hold versions.</summary>
    private SnapshotHolders Holders()
    {
        return new SnapshotHolders(Snapshots);
    }

    /// <summary>The reader of report bridges' owners.</summary>
    private BridgeOwnerReader Owners()
    {
        return new BridgeOwnerReader(Sources, ReportVersions, Reports);
    }

    /// <summary>The store of whole bridges.</summary>
    private BridgeStore BridgeStore()
    {
        return new BridgeStore(Bridges, BridgeVersions, BridgeMappings, Live(), _ids, _time);
    }

    /// <summary>The semantic revision.</summary>
    private SemanticRevision SemanticRevision()
    {
        return new SemanticRevision(DatabaseInfo, _time);
    }
}

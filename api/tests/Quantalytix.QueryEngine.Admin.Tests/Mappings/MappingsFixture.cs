using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
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
/// onto branches with live version 40 and frozen version 41, which a historical assumption set pins; an analytical bridge 50 from
/// branches to regions; and source <c>gl</c>'s report bridge 70 from branches into the report's rows.
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

    /// <summary>Its live version 2.</summary>
    public const long LiveMappingVersionId = 40;

    /// <summary>Its frozen version 1, which the historical assumption set pins.</summary>
    public const long FrozenMappingVersionId = 41;

    /// <summary>The analytical bridge from branches to regions.</summary>
    public const long BridgeId = 50;

    /// <summary>Its live version 1, <c>many_to_one</c> / <c>none</c>.</summary>
    public const long LiveBridgeVersionId = 51;

    /// <summary>Report source <c>gl</c>'s bridge from branches into report 60's rows.</summary>
    public const long ReportBridgeId = 70;

    /// <summary>Its live version, <c>many_to_many</c> / <c>replicate</c>.</summary>
    public const long ReportBridgeVersionId = 71;

    /// <summary>The active assumption set.</summary>
    public const long ActiveSetId = 5;

    /// <summary>The historical assumption set.</summary>
    public const long HistoricalSetId = 6;

    /// <summary>The first id the generator allocates.</summary>
    public const long FirstNewId = 900;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>The clock every service reads.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The id generator every service shares, so their ids never collide.</summary>
    private readonly SequentialIdGenerator _ids = new(FirstNewId);

    /// <summary>The publication set the workflow and the dimension reader share.</summary>
    private readonly PublicationSet _publicationSet = new();

    /// <summary>The dimension table.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(BranchDimensionId, DimensionKinds.Hierarchical) with { DimensionKey = "branch" },
        Dimension(RegionDimensionId, DimensionKinds.Categorical) with { DimensionKey = "region" },
        Dimension(ReportRows.ReportDimensionId, DimensionKinds.Hierarchical) with { DimensionKey = "balance_sheet_rows", DimensionPurpose = DimensionPurposes.Report },
        Dimension(TimeDimensionId, DimensionKinds.Time) with { DimensionKey = "business_date" });

    /// <summary>The dimension version table.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        Version(10, BranchDimensionId, 2, PublicationStatuses.Published),
        Version(11, BranchDimensionId, 1, PublicationStatuses.Retired),
        Version(20, RegionDimensionId, 1, PublicationStatuses.Published),
        Version(30, ReportRows.ReportDimensionId, 1, PublicationStatuses.Published));

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
        MappingRows.MappingVersion(LiveMappingVersionId, MappingSetId, 2, PublicationStatuses.Published),
        MappingRows.MappingVersion(FrozenMappingVersionId, MappingSetId, 1, PublicationStatuses.Retired));

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
        BridgeRows.BridgeVersion(LiveBridgeVersionId, BridgeId, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published),
        BridgeRows.BridgeVersion(ReportBridgeVersionId, ReportBridgeId, 1, Cardinalities.ManyToMany, AllocationModes.Replicate, PublicationStatuses.Published));

    /// <summary>The bridge mapping table: CA to WEST in the analytical bridge, US with its descendants into LOANS in the report bridge.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new(
        BridgeRows.Mapping(LiveBridgeVersionId, "CA", "WEST", 1m),
        BridgeRows.Mapping(ReportBridgeVersionId, "US", "LOANS", 1m) with { SourceSelection = SourceSelections.Descendants });

    /// <summary>The binding table.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new();

    /// <summary>The dataset version table: draft version 1000 of dataset 10.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(DatasetRows.Version(1000));

    /// <summary>The dataset table.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(new DatasetRecord { DatasetId = 10, DatasetKey = "bank.loans", DisplayName = "Loans", IsEnabled = true });

    /// <summary>The role table: the branch dimension's role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(Role(81, "branch", BranchDimensionId), Role(82, "region", RegionDimensionId));

    /// <summary>The field table: version 1000's branch code.</summary>
    public InMemoryTableGateway<FieldRecord> Fields { get; } = new(DatasetRows.ColumnField(90, 1000, "branch_code", "branch_code"));

    /// <summary>The report table: report 60.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(ReportRows.Report());

    /// <summary>The report source table: bridge source <c>gl</c>, which owns bridge 70.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(
        ReportRows.Source(80, "gl", 10) with { MappingMethod = ReportMappingMethods.Bridge, FieldKey = null, DimensionRoleId = 81, BridgeId = ReportBridgeId });

    /// <summary>The assumption set table: the active set and a historical one.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> AssumptionSets { get; } = new(
        new AssumptionSetRecord { AssumptionSetId = ActiveSetId, AssumptionSetKey = "active", DisplayName = "Active", Status = AssumptionSetStatuses.Active, CreatedAt = DateTime.UnixEpoch },
        new AssumptionSetRecord { AssumptionSetId = HistoricalSetId, AssumptionSetKey = "fy25", DisplayName = "FY25", Status = AssumptionSetStatuses.Historical, CreatedAt = DateTime.UnixEpoch });

    /// <summary>The dimension pin table.</summary>
    public InMemoryTableGateway<AssumptionSetDimensionRecord> DimensionPins { get; } = new();

    /// <summary>The mapping pin table: the active set pins the live version, the historical set the frozen one.</summary>
    public InMemoryTableGateway<AssumptionSetMappingRecord> MappingPins { get; } = new(
        new AssumptionSetMappingRecord { AssumptionSetId = ActiveSetId, MappingSetId = MappingSetId, MappingVersionId = LiveMappingVersionId },
        new AssumptionSetMappingRecord { AssumptionSetId = HistoricalSetId, MappingSetId = MappingSetId, MappingVersionId = FrozenMappingVersionId });

    /// <summary>The bridge pin table: the active set pins both bridges' live versions.</summary>
    public InMemoryTableGateway<AssumptionSetBridgeRecord> BridgePins { get; } = new(
        new AssumptionSetBridgeRecord { AssumptionSetId = ActiveSetId, BridgeId = BridgeId, BridgeVersionId = LiveBridgeVersionId },
        new AssumptionSetBridgeRecord { AssumptionSetId = ActiveSetId, BridgeId = ReportBridgeId, BridgeVersionId = ReportBridgeVersionId });

    /// <summary>The tenant's database info row.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 18,
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
        return new MappingSetSnapshotLoader(MappingSets, MappingVersions, Entries, Dimensions, MappingPins, Published(), References());
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
        return new MappingQueries(MappingSets, MappingVersions, Entries, References(), MappingLoader(), MappingRules(), _time);
    }

    /// <summary>The mapping set lifecycle commands over the version workflow.</summary>
    /// <returns>The commands.</returns>
    public MappingCommands MappingCommands()
    {
        return new MappingCommands(
            MappingSets, MappingVersions, Entries, Dimensions, Pins(), MappingDrafts(), new MappingVersionRemover(MappingVersions, Entries), MappingLoader(),
            Workflow(), Freezer(), SemanticRevision(), _ids);
    }

    /// <summary>The bridge snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public BridgeSnapshotLoader BridgeLoader()
    {
        return new BridgeSnapshotLoader(Bridges, BridgeVersions, BridgeMappings, Dimensions, BridgePins, Published(), References(), Owners());
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
        return new BridgeChanges(pipeline, Owners(), Bridges);
    }

    /// <summary>The bridge reads.</summary>
    /// <returns>The queries.</returns>
    public BridgeQueries BridgeQueries()
    {
        return new BridgeQueries(Bridges, BridgeVersions, BridgeMappings, Sources, References(), Owners(), BridgeLoader(), BridgeRules(), _time);
    }

    /// <summary>The analytical bridge lifecycle commands over the version workflow.</summary>
    /// <returns>The commands.</returns>
    public BridgeCommands BridgeCommands()
    {
        return new BridgeCommands(Bridges, BridgeVersions, BridgeMappings, Dimensions, BridgeStore(), BridgeLoader(), Freezer(), SemanticRevision(), _ids);
    }

    /// <summary>The reports module's hook that writes report bridges.</summary>
    /// <returns>The hook.</returns>
    public ReportBridges ReportBridges()
    {
        return new ReportBridges(Bridges, BridgeVersions, BridgeMappings, BridgeStore(), _ids);
    }

    /// <summary>Every mapping rule.</summary>
    private static ISnapshotRule<MappingSetSnapshot>[] MappingRuleList()
    {
        return [new DimensionVersionPublishedRule(), new SourceKeyUniqueRule(), new EntryMemberInVersionRule(), new EntryMemberAssignableRule()];
    }

    /// <summary>Every bridge rule.</summary>
    private static ISnapshotRule<BridgeSnapshot>[] BridgeRuleList()
    {
        return
        [
            new EndpointVersionsPublishedRule(), new MappingMembersInEndpointVersionsRule(), new PairUniqueRule(), new CardinalityManyToOneRule(),
            new AllocationNoneRule(), new AllocationPartitionRule(), new AllocationReplicateRule(), new DescendantsReportOnlyRule()
        ];
    }

    /// <summary>The readers both kinds share.</summary>
    private MappingReferenceReader References()
    {
        return new MappingReferenceReader(Dimensions, DimensionVersions, Bindings, DatasetVersions, Datasets, Roles, Fields, AssumptionSets);
    }

    /// <summary>The reader of dimensions' live versions.</summary>
    private PublishedDimensionReader Published()
    {
        return new PublishedDimensionReader(DimensionVersions, new InMemoryTableGateway<DimensionLevelRecord>(), Members, _publicationSet);
    }

    /// <summary>The reader of report bridges' owners.</summary>
    private BridgeOwnerReader Owners()
    {
        return new BridgeOwnerReader(Sources, Reports);
    }

    /// <summary>The mapping set drafts.</summary>
    private MappingDrafts MappingDrafts()
    {
        return new MappingDrafts(MappingVersions, Entries, _ids, _time);
    }

    /// <summary>The bridge drafts.</summary>
    private BridgeDrafts BridgeDrafts()
    {
        return new BridgeDrafts(BridgeVersions, BridgeMappings, _ids, _time);
    }

    /// <summary>The store of whole bridges.</summary>
    private BridgeStore BridgeStore()
    {
        return new BridgeStore(Bridges, BridgeVersions, BridgeMappings, BridgeDrafts(), Workflow(), Pins());
    }

    /// <summary>The assumption set pins.</summary>
    private AssumptionPins Pins()
    {
        return new AssumptionPins(AssumptionSets, DimensionPins, MappingPins, BridgePins);
    }

    /// <summary>The freeze of both living kinds.</summary>
    private LivingVersionFreezer Freezer()
    {
        return new LivingVersionFreezer(
            Stores(), Workflow(), [new MappingVersionCopier(MappingVersions, MappingDrafts()), new BridgeVersionCopier(BridgeVersions, BridgeDrafts())], _time);
    }

    /// <summary>
    /// The version workflow with the mapping and bridge rules as its publication rules and the handlers the API registers for both kinds:
    /// the predecessor retirer and the active set pinner.
    /// </summary>
    private VersionWorkflow Workflow()
    {
        var stores = Stores();
        IVersionLifecycleHandler[] handlers =
        [
            new PredecessorRetirer(VersionKinds.MappingVersion, stores, _time),
            new PredecessorRetirer(VersionKinds.BridgeVersion, stores, _time),
            new ActiveSetPinner(VersionKinds.MappingVersion, stores, Pins()),
            new ActiveSetPinner(VersionKinds.BridgeVersion, stores, Pins())
        ];
        IPublicationRule[] rules = [new MappingVersionRules(MappingLoader(), MappingRules()), new BridgeVersionRules(BridgeLoader(), BridgeRules())];
        return new VersionWorkflow(stores, rules, [], handlers, _publicationSet, SemanticRevision(), _time);
    }

    /// <summary>The version stores of both kinds.</summary>
    private VersionStoreRegistry Stores()
    {
        return new VersionStoreRegistry([new MappingVersionStore(MappingVersions), new BridgeVersionStore(BridgeVersions)]);
    }

    /// <summary>The semantic revision.</summary>
    private SemanticRevision SemanticRevision()
    {
        return new SemanticRevision(DatabaseInfo, _time);
    }
}

using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// In-memory tables of one tenant and the dimension module's loader, rules and change-set pipeline over them, so tests run the
/// module as the API does. Seeds a hierarchical user dimension with a live version 1, a frozen version 0 and a role.
/// </summary>
internal sealed class DimensionFixture
{
    /// <summary>The seeded dimension.</summary>
    public const long DimensionId = 100;

    /// <summary>Its role.</summary>
    public const long RoleId = 110;

    /// <summary>Its live version 1.</summary>
    public const long LiveVersionId = 101;

    /// <summary>Its frozen version, numbered 0 so the live one is 1.</summary>
    public const long FrozenVersionId = 102;

    /// <summary>The first id the generator allocates.</summary>
    public const long FirstNewId = 900;

    /// <summary>The active assumption set.</summary>
    public const long ActiveSetId = 5;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>The dimension table.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(DimensionRows.Dimension(DimensionId, DimensionKinds.Hierarchical));

    /// <summary>The role table: the seeded dimension's role, and another dimension's.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(DimensionRows.Role(RoleId, "branch", DimensionId), DimensionRows.Role(210, "product", 200));

    /// <summary>The version table.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> Versions { get; } = new(
        DimensionRows.Version(LiveVersionId, DimensionId, 1, PublicationStatuses.Published),
        DimensionRows.Version(FrozenVersionId, DimensionId, 0, PublicationStatuses.Retired));

    /// <summary>The level table.</summary>
    public InMemoryTableGateway<DimensionLevelRecord> Levels { get; } = new();

    /// <summary>The member table.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new();

    /// <summary>The range band table.</summary>
    public InMemoryTableGateway<RangeBandRecord> Bands { get; } = new();

    /// <summary>The closure table.</summary>
    public InMemoryTableGateway<DimensionClosureRecord> Closure { get; } = new();

    /// <summary>The binding table.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new();

    /// <summary>The dataset version table.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new();

    /// <summary>The dataset table.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new();

    /// <summary>The mapping set table.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new();

    /// <summary>The mapping version table.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new();

    /// <summary>The mapping entry table.</summary>
    public InMemoryTableGateway<DimensionMappingRecord> MappingEntries { get; } = new();

    /// <summary>The bridge table.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new();

    /// <summary>The bridge version table.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new();

    /// <summary>The bridge mapping table.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new();

    /// <summary>The report table.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new();

    /// <summary>The calculation dimension table.</summary>
    public InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions { get; } = new();

    /// <summary>The report scenario table.</summary>
    public InMemoryTableGateway<ReportScenarioRecord> Scenarios { get; } = new();

    /// <summary>The report source table.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new();

    /// <summary>The assumption set table: the active set.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> AssumptionSets { get; } = new(new AssumptionSetRecord
    {
        AssumptionSetId = ActiveSetId,
        AssumptionSetKey = "active",
        DisplayName = "Active",
        Status = AssumptionSetStatuses.Active,
        CreatedAt = DateTime.UnixEpoch
    });

    /// <summary>The assumption set dimension pin table.</summary>
    public InMemoryTableGateway<AssumptionSetDimensionRecord> Pins { get; } = new();

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

    /// <summary>Gives the seeded dimension a profile, making it a system dimension.</summary>
    /// <param name="profileKey">A profile of the registry.</param>
    /// <returns>A task that completes when the row is updated.</returns>
    public Task FollowProfileAsync(string profileKey)
    {
        var dimension = Dimensions.All.Single();
        return Dimensions.UpdateAsync(dimension with { DefinitionOrigin = DefinitionOrigins.System, IsRemovable = false, DimensionProfileKey = profileKey }, CancellationToken.None);
    }

    /// <summary>The snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public DimensionSnapshotLoader Loader()
    {
        var names = new BindingNames(Roles, new InMemoryTableGateway<FieldRecord>());
        var usage = new DimensionUsageReader(
            Bindings, DatasetVersions, Datasets, MappingSets, MappingVersions, MappingEntries, Bridges, BridgeVersions, BridgeMappings, Reports,
            CalculationDimensions, Scenarios, Sources, AssumptionSets, Pins, names);
        var consumers = new DimensionConsumers(
            Datasets, DatasetVersions, Bindings, Roles, MappingSets, MappingVersions, MappingEntries, Bridges, BridgeVersions, BridgeMappings, Reports,
            CalculationDimensions, names);
        return new DimensionSnapshotLoader(Dimensions, Roles, Versions, Levels, Members, Bands, usage, consumers);
    }

    /// <summary>The dimension change-set pipeline with every dimension rule, as the API registers them.</summary>
    /// <returns>The pipeline.</returns>
    public ChangeSetPipeline<DimensionSnapshot, DimensionOperation, DimensionEditState> Pipeline()
    {
        return new ChangeSetPipeline<DimensionSnapshot, DimensionOperation, DimensionEditState>(
            new DimensionAggregate(Loader(), Dimensions, Roles, Versions, Levels, Members, Bands, Closure),
            Rules(),
            [new DimensionOperationValidator()],
            new SequentialIdGenerator(FirstNewId),
            new SemanticRevision(DatabaseInfo, new FakeTimeProvider()));
    }

    /// <summary>
    /// The lifecycle commands over the version workflow, with the dimension rules as its publication rule and the handlers the API
    /// registers for dimension versions: the predecessor retirer, the active set pinner and the closure builder.
    /// </summary>
    /// <returns>The commands.</returns>
    public DimensionCommands Commands()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var ids = new SequentialIdGenerator(FirstNewId);
        var stores = new VersionStoreRegistry([new DimensionVersionStore(Versions)]);
        var pins = new AssumptionPins(AssumptionSets, Pins, new InMemoryTableGateway<AssumptionSetMappingRecord>(), new InMemoryTableGateway<AssumptionSetBridgeRecord>());
        var revision = new SemanticRevision(DatabaseInfo, time);
        var loader = Loader();
        var workflow = new VersionWorkflow(
            stores,
            [new DimensionVersionRules(loader, new DimensionRules(Rules()))],
            [],
            [
                new PredecessorRetirer(VersionKinds.DimensionVersion, stores, time),
                new ActiveSetPinner(VersionKinds.DimensionVersion, stores, pins),
                new ClosureBuilder(Members, Closure)
            ],
            new PublicationSet(),
            revision,
            time);
        var drafts = new DimensionDrafts(Versions, new DimensionVersionCloner(Levels, Members, Bands, ids), ids, time);
        var freezer = new LivingVersionFreezer(stores, workflow, [new DimensionVersionCopier(Versions, drafts)], time);
        return new DimensionCommands(
            Dimensions, Roles, Versions, Members, drafts, new DimensionVersionRemover(Versions, Levels, Members, Closure, Bands), loader, workflow, freezer, pins, revision, ids);
    }

    /// <summary>Every dimension rule, in the API's registration order.</summary>
    /// <returns>The rules.</returns>
    public static IReadOnlyList<ISnapshotRule<DimensionSnapshot>> Rules()
    {
        return
        [
            new RangeDataTypeMatchesKindRule(),
            new LevelKeyUniqueRule(),
            new MemberKeyUniqueRule(),
            new MemberParentInVersionRule(),
            new MemberLevelInVersionRule(),
            new HierarchyAcyclicRule(),
            new BandBoundaryFamilyRule(),
            new BandBoundsOrderedRule(),
            new BandsNonOverlappingRule(),
            new BandOpenBoundsAtEndsRule(),
            new BandMemberInVersionRule(),
            new DimensionConsumersResolveRule(),
            new ProfileRequiredKeysPresentRule(),
            new ProfilePropertiesValidRule()
        ];
    }
}

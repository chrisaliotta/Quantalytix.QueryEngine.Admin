using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.SourceObjects;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// In-memory tables and physical catalog of one tenant, and the dataset module's loader, rules, change set and commands over them,
/// so tests run the module as the API does. Seeds dataset 10 with a frozen version in snapshot 2 and a live version, both reading the
/// unmanaged table <c>loan_daily</c>; the managed table <c>staging_loans</c> is registered but read by no version.
/// </summary>
internal sealed class DatasetFixture
{
    /// <summary>The seeded dataset.</summary>
    public const long DatasetId = 10;

    /// <summary>Its version in the frozen snapshot.</summary>
    public const long FrozenVersionId = 99;

    /// <summary>Its live version, which change sets edit.</summary>
    public const long LiveVersionId = 100;

    /// <summary>A role of the branch dimension.</summary>
    public const long BranchRoleId = 7100;

    /// <summary>The branch dimension.</summary>
    public const long BranchDimensionId = 7000;

    /// <summary>The first id the generator allocates.</summary>
    public const long FirstNewId = 900;

    /// <summary>The unmanaged table both versions read.</summary>
    public const string LoanTable = "loan_daily";

    /// <summary>The managed table the API created.</summary>
    public const string ManagedTable = "staging_loans";

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>The clock.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The dataset table.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(new DatasetRecord { DatasetId = DatasetId, DatasetKey = "bank.loans", DisplayName = "Loans" });

    /// <summary>The dataset version table.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> Versions { get; } = new(
        DatasetRows.Version(FrozenVersionId) with { SnapshotId = SnapshotRows.FrozenId },
        DatasetRows.Version(LiveVersionId));

    /// <summary>The snapshot table: the live snapshot and one frozen snapshot.</summary>
    public InMemoryTableGateway<SnapshotRecord> Snapshots { get; } = new(SnapshotRows.Live(), SnapshotRows.Frozen());

    /// <summary>The field table.</summary>
    public InMemoryTableGateway<FieldRecord> Fields { get; } = new();

    /// <summary>The grain table.</summary>
    public InMemoryTableGateway<DatasetGrainRecord> Grains { get; } = new();

    /// <summary>The grain component table.</summary>
    public InMemoryTableGateway<DatasetGrainFieldRecord> GrainFields { get; } = new();

    /// <summary>The binding table.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new();

    /// <summary>The time binding table.</summary>
    public InMemoryTableGateway<TimeBindingRecord> TimeBindings { get; } = new();

    /// <summary>The measure table.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new();

    /// <summary>The measure component table.</summary>
    public InMemoryTableGateway<MeasureComponentRecord> Components { get; } = new();

    /// <summary>The measure dependency table.</summary>
    public InMemoryTableGateway<MeasureDependencyRecord> Dependencies { get; } = new();

    /// <summary>The measure time policy table.</summary>
    public InMemoryTableGateway<MeasureTimePolicyRecord> TimePolicies { get; } = new();

    /// <summary>The managed-table registry.</summary>
    public InMemoryTableGateway<ManagedSourceObjectRecord> Managed { get; } = new(new ManagedSourceObjectRecord
    {
        ManagedSourceObjectId = 50,
        SourceObject = ManagedTable,
        SourceObjectKind = SourceObjectKinds.Table,
        CreatedByDatasetId = DatasetId,
        CreatedAt = DateTime.UnixEpoch
    });

    /// <summary>The tenant's database info row.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 21,
        SemanticRevision = StartingRevision,
        UpdatedAt = DateTime.UnixEpoch
    });

    /// <summary>The role table: the branch role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(DimensionRows.Role(BranchRoleId, "branch", BranchDimensionId));

    /// <summary>The dimension table: the branch dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(DimensionRows.Dimension(BranchDimensionId, DimensionKinds.Categorical));

    /// <summary>The dimension version table: the branch dimension's live version.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(DimensionRows.Version(7001, BranchDimensionId));

    /// <summary>The physical tables, views and columns.</summary>
    public FixedSourceObjectCatalog Catalog { get; } = new FixedSourceObjectCatalog()
        .With(SourceObjectKinds.Table, LoanTable, ("loan_id", "bigint(20)"), ("branch_code", "varchar(16)"), ("balance", "decimal(20,2)"), ("attributes", "json"), ("sketch", "hll"))
        .With(SourceObjectKinds.Table, ManagedTable, ("loan_id", "bigint(20)"));

    /// <summary>The materialized views.</summary>
    public InMemoryMaterializedViews Views { get; } = new();

    /// <summary>The managed-table DDL the commands and commits run.</summary>
    public RecordingManagedTables ManagedTables => _managedTables ??= new RecordingManagedTables(Catalog);

    /// <summary>The tenant's semantic revision.</summary>
    public long Revision => DatabaseInfo.All.Single().SemanticRevision;

    /// <summary>The lazily created managed-table double.</summary>
    private RecordingManagedTables? _managedTables;

    /// <summary>Points a version at the managed table.</summary>
    /// <param name="versionId">The dataset version; the live one by default.</param>
    /// <returns>A task that completes when the row is updated.</returns>
    public Task UseManagedTableAsync(long versionId = LiveVersionId)
    {
        var version = Versions.All.Single(row => row.DatasetVersionId == versionId);
        return Versions.UpdateAsync(version with { SourceObject = ManagedTable }, CancellationToken.None);
    }

    /// <summary>The physical catalog the module reads.</summary>
    /// <returns>The directory.</returns>
    public SourceObjectDirectory Directory()
    {
        return new SourceObjectDirectory(Catalog, Managed);
    }

    /// <summary>The snapshot loader over the tables.</summary>
    /// <returns>The loader.</returns>
    public DatasetSnapshotLoader Loader()
    {
        return new DatasetSnapshotLoader(
            Datasets,
            Versions,
            Content(),
            Roles,
            Dimensions,
            DimensionVersions,
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<MappingVersionRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<BridgeVersionRecord>(),
            Live(),
            Directory());
    }

    /// <summary>The dataset change-set pipeline with every dataset rule, as the API registers them.</summary>
    /// <returns>The pipeline.</returns>
    public ChangeSetPipeline<DatasetSnapshot, DatasetOperation, DatasetVersion> Pipeline()
    {
        return new ChangeSetPipeline<DatasetSnapshot, DatasetOperation, DatasetVersion>(
            new DatasetAggregate(
                Loader(), new DatasetVersionReader(Views), Datasets, Versions, Fields, Grains, GrainFields, Bindings, TimeBindings, Measures, Components, Dependencies,
                TimePolicies, Managed, new DatasetSchemaChanges(ManagedTables, Materializations())),
            Rules(),
            [new DatasetOperationValidator()],
            new SequentialIdGenerator(FirstNewId),
            SemanticRevision());
    }

    /// <summary>The change set with its materialization requests.</summary>
    /// <returns>The service.</returns>
    public DatasetChanges Changes()
    {
        return new DatasetChanges(Pipeline(), Materializations(), Queries());
    }

    /// <summary>The materialization commands.</summary>
    /// <returns>The service.</returns>
    public DatasetMaterializations Materializations()
    {
        return new DatasetMaterializations(Versions, Fields, Live(), Views, SemanticRevision());
    }

    /// <summary>The reads.</summary>
    /// <returns>The service.</returns>
    public DatasetQueries Queries()
    {
        return new DatasetQueries(Datasets, Versions, Live(), new SnapshotHolders(Snapshots), Directory(), Loader(), new DatasetVersionReader(Views));
    }

    /// <summary>The create and delete commands.</summary>
    /// <returns>The commands.</returns>
    public DatasetCommands Commands()
    {
        return new DatasetCommands(
            Datasets,
            Versions,
            Fields,
            Managed,
            Content(),
            Live(),
            new SnapshotHolders(Snapshots),
            Queries(),
            Directory(),
            ManagedTables,
            new DatasetTableDrops(Versions, Managed, Directory(), ManagedTables, Views),
            SemanticRevision(),
            new SequentialIdGenerator(FirstNewId),
            Time);
    }

    /// <summary>The copier capture and restore use.</summary>
    /// <returns>The copier.</returns>
    public DatasetVersionCopier Copier()
    {
        return new DatasetVersionCopier(Versions, Content(), new SequentialIdGenerator(FirstNewId), Time);
    }

    /// <summary>The remover snapshot deletes and restores use.</summary>
    /// <returns>The remover.</returns>
    public DatasetVersionRemover Remover()
    {
        return new DatasetVersionRemover(Datasets, Versions, Content());
    }

    /// <summary>The live snapshot over the snapshot table.</summary>
    /// <returns>The reader.</returns>
    public LiveSnapshot Live()
    {
        return new LiveSnapshot(Snapshots);
    }

    /// <summary>
    /// Every dataset rule, in the API's registration order, except the report rules over the dataset, which need no reports here
    /// because the fixture seeds none.
    /// </summary>
    /// <returns>The rules.</returns>
    public IReadOnlyList<ISnapshotRule<DatasetSnapshot>> Rules()
    {
        var dimensionKeys = new SnapshotDimensionReader(DimensionVersions, new InMemoryTableGateway<DimensionLevelRecord>(), new InMemoryTableGateway<DimensionMemberRecord>());
        return
        [
            new SourceObjectExistsRule(),
            new VersionSupportsAModeRule(),
            new DefaultTimeBindingInVersionRule(),
            new FieldKeyUniqueRule(),
            new FieldColumnsExistRule(),
            new ExpressionFieldReferencesRule(),
            new ExpressionFieldAcyclicRule(),
            new ExpressionsFitLanguageRule(),
            new GrainKeyUniqueRule(),
            new GrainFieldsInVersionRule(),
            new SeriesGrainExcludesTimeFieldRule(),
            new BindingFieldsInVersionRule(),
            new BindingSourceFieldGroupableRule(),
            new BoundRolesDistinctRule(),
            new MappedBindingMappingSetDimensionRule(),
            new RangeBindingDimensionKindRule(),
            new TimeBindingDimensionKindRule(),
            new BridgeBindingRouteRule(),
            new BoundDimensionsResolvableRule(),
            new MappedBindingsResolvableRule(),
            new BridgeBindingsResolvableRule(),
            new UnknownMembersInVersionRule(dimensionKeys),
            new IntervalEstimatesForFinerPeriodsRule(),
            new MeasureKeyUniqueRule(),
            new ComponentFieldsInVersionRule(),
            new ComponentRolesMatchKindRule(),
            new FormulaReferencesKnownMeasuresRule(),
            new FormulaAcyclicRule(),
            new CompoundInputsRule(),
            new TimePolicySeriesGrainRule(),
            new TimePolicyTimeBindingInVersionRule()
        ];
    }

    /// <summary>The version content store over the tables.</summary>
    private DatasetVersionContentStore Content()
    {
        return new DatasetVersionContentStore(Fields, Grains, GrainFields, Bindings, TimeBindings, Measures, Components, Dependencies, TimePolicies);
    }

    /// <summary>The semantic revision over the database info row.</summary>
    private SemanticRevision SemanticRevision()
    {
        return new SemanticRevision(DatabaseInfo, Time);
    }
}

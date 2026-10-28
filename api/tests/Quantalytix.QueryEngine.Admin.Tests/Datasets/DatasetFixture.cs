using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.SourceObjects;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// In-memory tables and physical catalog of one tenant, and the dataset module's loader, rules, change set and commands over them,
/// so tests run the module as the API does. Seeds dataset 10 with a published version 1 and a draft version 2, both reading the
/// unmanaged table <c>loan_daily</c>; the managed table <c>staging_loans</c> is registered but read by no version.
/// </summary>
internal sealed class DatasetFixture
{
    /// <summary>The seeded dataset.</summary>
    public const long DatasetId = 10;

    /// <summary>Its published version 1.</summary>
    public const long PublishedVersionId = 99;

    /// <summary>Its draft version 2.</summary>
    public const long DraftVersionId = 100;

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
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(new DatasetRecord { DatasetId = DatasetId, DatasetKey = "bank.loans", DisplayName = "Loans", IsEnabled = true });

    /// <summary>The dataset version table.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> Versions { get; } = new(
        DatasetRows.Version(PublishedVersionId) with { VersionNumber = 1, Status = PublicationStatuses.Published, PublishedAt = DateTime.UnixEpoch },
        DatasetRows.Version(DraftVersionId) with { VersionNumber = 2 });

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

    /// <summary>The release manifest dataset table.</summary>
    public InMemoryTableGateway<ReleaseDatasetRecord> ReleaseDatasets { get; } = new();

    /// <summary>The semantic release table.</summary>
    public InMemoryTableGateway<SemanticReleaseRecord> Releases { get; } = new();

    /// <summary>The tenant's database info row.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 18,
        SemanticRevision = StartingRevision,
        UpdatedAt = DateTime.UnixEpoch
    });

    /// <summary>The role table: the branch role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(DimensionRows.Role(BranchRoleId, "branch", BranchDimensionId));

    /// <summary>The dimension table: the branch dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(DimensionRows.Dimension(BranchDimensionId, DimensionKinds.Categorical));

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

    /// <summary>Points the draft at the managed table.</summary>
    /// <returns>A task that completes when the row is updated.</returns>
    public Task UseManagedTableAsync()
    {
        var draft = Versions.All.Single(version => version.DatasetVersionId == DraftVersionId);
        return Versions.UpdateAsync(draft with { SourceObject = ManagedTable }, CancellationToken.None);
    }

    /// <summary>Makes a release pin a version, the active one when asked.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="active">Whether the release is the tenant's active one.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    public async Task PinAsync(long versionId, bool active)
    {
        await Releases.InsertAsync(new SemanticReleaseRecord { SemanticReleaseId = 600, ReleaseKey = "r1", Status = PublicationStatuses.Published, CreatedAt = DateTime.UnixEpoch }, CancellationToken.None);
        await ReleaseDatasets.InsertAsync(new ReleaseDatasetRecord { SemanticReleaseId = 600, DatasetId = DatasetId, DatasetVersionId = versionId }, CancellationToken.None);
        if (active) await DatabaseInfo.UpdateAsync(DatabaseInfo.All.Single() with { ActiveSemanticReleaseId = 600 }, CancellationToken.None);
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
            Datasets, Versions, Content(), ReleaseReader(), Roles, Dimensions, new InMemoryTableGateway<MappingSetRecord>(), new InMemoryTableGateway<BridgeRecord>(), Directory());
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
        return new DatasetMaterializations(Versions, Fields, Views, SemanticRevision());
    }

    /// <summary>The reads.</summary>
    /// <returns>The service.</returns>
    public DatasetQueries Queries()
    {
        return new DatasetQueries(Datasets, Versions, ReleaseReader(), Directory(), Loader(), new DatasetVersionReader(Views), new DatasetRules(Rules()), Time);
    }

    /// <summary>The lifecycle commands over the version workflow, with the dataset rules as its <c>dataset_version</c> publication rule.</summary>
    /// <returns>The commands.</returns>
    public DatasetCommands Commands()
    {
        var ids = new SequentialIdGenerator(FirstNewId);
        var stores = new VersionStoreRegistry([new DatasetVersionStore(Versions)]);
        var workflow = new VersionWorkflow(stores, [new DatasetVersionRules(Loader(), new DatasetRules(Rules()))], [], [], new PublicationSet(), SemanticRevision(), Time);
        return new DatasetCommands(
            Datasets, Versions, Fields, Managed, Content(), new DatasetVersionCloner(Versions, Content(), ids, Time), ReleaseReader(), Queries(), Directory(), ManagedTables,
            workflow, ids, Time);
    }

    /// <summary>Every dataset rule, in the API's registration order.</summary>
    /// <returns>The rules.</returns>
    public IReadOnlyList<ISnapshotRule<DatasetSnapshot>> Rules()
    {
        var published = new PublishedDimensionReader(
            new InMemoryTableGateway<DimensionVersionRecord>(), new InMemoryTableGateway<DimensionLevelRecord>(), new InMemoryTableGateway<DimensionMemberRecord>(), new PublicationSet());
        return
        [
            new SourceObjectExistsRule(),
            new VersionSupportsAModeRule(),
            new DefaultTimeBindingInVersionRule(),
            new FieldKeyUniqueRule(),
            new FieldColumnsExistRule(),
            new ExpressionFieldReferencesRule(),
            new ExpressionFieldAcyclicRule(),
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
            new UnknownMembersInVersionRule(published),
            new IntervalEstimatesForFinerPeriodsRule(),
            new MeasureKeyUniqueRule(),
            new ComponentFieldsInVersionRule(),
            new ComponentRolesMatchKindRule(),
            new FormulaReferencesKnownMeasuresRule(),
            new FormulaAcyclicRule(),
            new TimePolicySeriesGrainRule(),
            new TimePolicyTimeBindingInVersionRule()
        ];
    }

    /// <summary>The version content store over the tables.</summary>
    private DatasetVersionContentStore Content()
    {
        return new DatasetVersionContentStore(Fields, Grains, GrainFields, Bindings, TimeBindings, Measures, Components, Dependencies, TimePolicies);
    }

    /// <summary>The release pin reader over the tables.</summary>
    private DatasetReleaseReader ReleaseReader()
    {
        return new DatasetReleaseReader(ReleaseDatasets, Releases, DatabaseInfo);
    }

    /// <summary>The semantic revision over the database info row.</summary>
    private SemanticRevision SemanticRevision()
    {
        return new SemanticRevision(DatabaseInfo, Time);
    }
}

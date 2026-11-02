using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.SourceObjects;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Support.CatalogRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// The tables of one report and the reports module's services over them, wired as the API registers them. Disabled report 60 reads model 40, which has variable
/// <c>balance</c>, and owns report dimension 500, whose one version 501 holds the rows TOTAL, a group, with LOANS
/// and CASH, data rows, under it. Its bridge source <c>gl</c> reads GL's <c>balance</c>, which ignores bridge factors, by
/// <c>as_of_date</c> and classifies GL's rows into account dimension 800, whose live version 801 holds the folder LENDING over
/// accounts 1001 and 1003, and account 1002; the source's many-to-many bridge 90 maps 1001 to LOANS and 1002 to CASH in its one
/// version 91. Profile <c>base</c>, the report's default, picks the source for balance, and column <c>amount</c> returns balance,
/// so the report passes every report rule. The live snapshot holds GL version 11 and versions 501, 801 and 91.
/// </summary>
internal sealed class ReportFixture
{
    /// <summary>The report dimension's one version, which holds the rows.</summary>
    public const long StructureVersionId = 501;

    /// <summary>The analytical dimension of accounts, which the source's role belongs to.</summary>
    public const long AccountDimensionId = 800;

    /// <summary>The live version of the account dimension.</summary>
    public const long AccountVersionId = 801;

    /// <summary>The account role GL binds.</summary>
    public const long AccountRoleId = 85;

    /// <summary>The GL dataset.</summary>
    public const long GlDatasetId = 1;

    /// <summary>GL's live version.</summary>
    public const long GlVersionId = 11;

    /// <summary>The table GL version 11 reads.</summary>
    public const string GlTable = "gl_balances";

    /// <summary>The bridge source.</summary>
    public const long SourceId = 70;

    /// <summary>The source's bridge from accounts into the report dimension.</summary>
    public const long BridgeId = 90;

    /// <summary>The bridge's one version, which holds the item mappings.</summary>
    public const long BridgeVersionId = 91;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>Allocates the IDs of new rows.</summary>
    private readonly SequentialIdGenerator _ids = new(5000);

    /// <summary>The clock.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The database info.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 19,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The live snapshot.</summary>
    public InMemoryTableGateway<SnapshotRecord> Snapshots { get; } = new(SnapshotRows.Live());

    /// <summary>The report.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(ReportRows.Report());

    /// <summary>The report's disabled live version, which defaults to profile base.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(ReportRows.Version() with { IsEnabled = false, DefaultProfileKey = "base" });

    /// <summary>The report dimension and the account dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(ReportRows.ReportDimensionId, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report },
        Dimension(AccountDimensionId, "account"));

    /// <summary>The live versions of both dimensions.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        DimensionRows.Version(StructureVersionId, ReportRows.ReportDimensionId),
        DimensionRows.Version(AccountVersionId, AccountDimensionId));

    /// <summary>No levels.</summary>
    public InMemoryTableGateway<DimensionLevelRecord> Levels { get; } = new();

    /// <summary>The rows TOTAL, a group, and LOANS and CASH, data rows under it; and the accounts, 1001 and 1003 in folder LENDING.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, StructureVersionId, "TOTAL") with { IsAssignable = false },
        DimensionRows.Member(2, StructureVersionId, "LOANS", parentId: 1),
        DimensionRows.Member(3, StructureVersionId, "CASH", parentId: 1) with { SortOrder = 1 },
        DimensionRows.Member(14, AccountVersionId, "LENDING") with { IsAssignable = false },
        DimensionRows.Member(11, AccountVersionId, "1001", parentId: 14),
        DimensionRows.Member(12, AccountVersionId, "1002"),
        DimensionRows.Member(13, AccountVersionId, "1003", parentId: 14));

    /// <summary>The closure rows the member writes rebuild.</summary>
    public InMemoryTableGateway<DimensionClosureRecord> Closure { get; } = new();

    /// <summary>The account role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(DimensionRows.Role(AccountRoleId, "account", AccountDimensionId));

    /// <summary>The source's bridge.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(BridgeRows.Bridge(BridgeId, AccountDimensionId, ReportRows.ReportDimensionId));

    /// <summary>The bridge's live version, which replicates each item into every row its rules name.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(
        BridgeRows.BridgeVersion(BridgeVersionId, BridgeId, Cardinalities.ManyToMany, AllocationModes.Replicate));

    /// <summary>The item mappings: 1001 to LOANS and 1002 to CASH.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new(
        BridgeRows.Mapping(BridgeVersionId, "1001", "LOANS", 1m),
        BridgeRows.Mapping(BridgeVersionId, "1002", "CASH", 1m));

    /// <summary>The bridge source <c>gl</c>.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(ReportRows.Source(SourceId, "gl", GlDatasetId) with
    {
        MappingMethod = ReportMappingMethods.Bridge,
        FieldKey = null,
        DimensionRoleId = AccountRoleId,
        BridgeId = BridgeId
    });

    /// <summary>The source's supply of balance.</summary>
    public InMemoryTableGateway<ReportSourceVariableRecord> SourceVariables { get; } = new(ReportRows.SourceVariable(SourceId, "balance"));

    /// <summary>Profile base.</summary>
    public InMemoryTableGateway<SourceProfileRecord> Profiles { get; } = new(ReportRows.Profile(75, "base"));

    /// <summary>Profile base's pick of the source for balance.</summary>
    public InMemoryTableGateway<SourceProfileVariableRecord> Picks { get; } = new(ReportRows.Pick(75, "balance", SourceId));

    /// <summary>Column amount, which returns balance.</summary>
    public InMemoryTableGateway<ReportColumnRecord> Columns { get; } = new(ReportRows.Column(1, "amount", ReportValueKinds.Variable, "balance", 1));

    /// <summary>The overlay rows; none at first.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new();

    /// <summary>The member options; none at first.</summary>
    public InMemoryTableGateway<ReportMemberOptionRecord> MemberOptions { get; } = new();

    /// <summary>The report's styles; none at first.</summary>
    public InMemoryTableGateway<ReportStyleRecord> Styles { get; } = new();

    /// <summary>The rows' parameter values; none at first.</summary>
    public InMemoryTableGateway<ReportRowParameterRecord> RowParameters { get; } = new();

    /// <summary>Model 40's parameters; none at first.</summary>
    public InMemoryTableGateway<ModelParameterRecord> Parameters { get; } = new();

    /// <summary>The variable scopes; none at first.</summary>
    public InMemoryTableGateway<ReportVariableScopeRecord> Scopes { get; } = new();

    /// <summary>The scenarios; none at first.</summary>
    public InMemoryTableGateway<ReportScenarioRecord> Scenarios { get; } = new();

    /// <summary>The items the bridge source leaves out; none at first.</summary>
    public InMemoryTableGateway<ReportSourceLeftOutRecord> LeftOut { get; } = new();

    /// <summary>The calculation dimensions; none.</summary>
    public InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions { get; } = new();

    /// <summary>Model 40.</summary>
    public InMemoryTableGateway<CalculationModelRecord> Models { get; } = new(Model());

    /// <summary>Model 40's live version.</summary>
    public InMemoryTableGateway<CalculationModelVersionRecord> ModelVersions { get; } = new(Version());

    /// <summary>The balance variable.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance"));

    /// <summary>No calculated measures.</summary>
    public InMemoryTableGateway<CalculatedMeasureRecord> CalculatedMeasures { get; } = new();

    /// <summary>The GL dataset.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(Dataset(GlDatasetId, "gl"));

    /// <summary>GL's live version 11 over the table <c>gl_balances</c>.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(
        DatasetRows.Version(GlVersionId) with { DatasetId = GlDatasetId, SourceObject = GlTable });

    /// <summary>The tenant database's objects and the values their columns hold; none at first.</summary>
    public FixedSourceObjectCatalog Catalog { get; } = new();

    /// <summary>GL's fields: the account code, the as-of date, and a disabled memo.</summary>
    public InMemoryTableGateway<FieldRecord> Fields { get; } = new(
        DatasetRows.ColumnField(1, GlVersionId, "gl_account", "gl_account"),
        DatasetRows.ColumnField(2, GlVersionId, "as_of_date", "as_of_date", DataTypes.Date),
        DatasetRows.ColumnField(3, GlVersionId, "memo", "memo") with { IsEnabled = false });

    /// <summary>GL's bindings: the account role, directly, and the as-of date.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new(
        Binding(31, GlVersionId, AccountRoleId, AccountDimensionId, BindingKinds.Direct) with { SourceFieldId = 1 },
        Binding(32, GlVersionId, null, 700, BindingKinds.Time) with { SourceFieldId = 2 });

    /// <summary>The time extension of GL's as-of binding.</summary>
    public InMemoryTableGateway<TimeBindingRecord> TimeBindings { get; } = new(DatasetRows.TimeBinding(32));

    /// <summary>GL's balance measure.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(DatasetRows.Measure(41, GlVersionId, "balance", MeasureKinds.Aggregate));

    /// <summary>The report change set, with the gate and the findings, as the API registers it.</summary>
    /// <returns>The change set over these tables.</returns>
    public ReportChanges Changes()
    {
        var pipeline = new ChangeSetPipeline<ReportSnapshot, ReportOperation, ReportDocument>(
            new ReportAggregate(
                Loader(), ReportVersions, Members, Closure, Lines, MemberOptions, Styles, Columns, Profiles, Picks, CalculationDimensions, Scenarios, Scopes,
                RowParameters, new ReportSourceWrites(Sources, ReportBridges()), SourceVariables, LeftOut),
            [new ReportGateRule(Rules()), new ReportFindingsRule(Rules())],
            [new ReportOperationValidator()],
            _ids,
            Revision());
        return new ReportChanges(pipeline, Diagnostics());
    }

    /// <summary>The report reads.</summary>
    /// <returns>The queries over these tables.</returns>
    public ReportQueries Queries()
    {
        return new ReportQueries(Versions(), Models, Profiles, Sources, Live(), Loader(), Rules(), Diagnostics());
    }

    /// <summary>The create and enable commands.</summary>
    /// <returns>The commands over these tables.</returns>
    public ReportCommands Commands()
    {
        var models = new ModelSnapshotLoader(
            Models, ModelVersions, Variables, CalculatedMeasures,
            new MetricAttributionService(new InMemoryTableGateway<MetricAttributionRecord>(), new InMemoryTableGateway<MetricAttributionDriverRecord>()),
            ParameterService(), Versions(), GraphReader(), Live());
        return new ReportCommands(
            Reports, ReportVersions, Dimensions, DimensionService(), DimensionKeys(), models, Loader(), Rules(), Queries(), Changes(), Live(), Revision(), _ids, _time);
    }

    /// <summary>The report delete.</summary>
    /// <returns>The remover over these tables.</returns>
    public ReportRemover Remover()
    {
        return new ReportRemover(
            Versions(), ReportVersions, Sources, VersionRemover(), ReportBridges(), DimensionService(), DimensionVersions, new SnapshotHolders(Snapshots), Revision());
    }

    /// <summary>The parameters of model 40.</summary>
    /// <returns>The service over these tables.</returns>
    public ModelParameterService ParameterService()
    {
        return new ModelParameterService(Parameters);
    }

    /// <summary>The tenant's semantic revision.</summary>
    public long SemanticRevisionNumber => DatabaseInfo.All.Single().SemanticRevision;

    /// <summary>Sets whether the report's live version is enabled, as the enable command would once its rules pass.</summary>
    /// <param name="isEnabled">Whether the report is enabled.</param>
    /// <returns>A task that completes when the version is stored.</returns>
    public async Task SetEnabledAsync(bool isEnabled)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var version = await ReportVersions.GetAsync(new { ReportRows.ReportVersionId }, cancellationToken);
        await ReportVersions.UpdateAsync(version with { IsEnabled = isEnabled }, cancellationToken);
    }

    /// <summary>The live snapshot over the snapshot table.</summary>
    /// <returns>The reader.</returns>
    public LiveSnapshot Live()
    {
        return new LiveSnapshot(Snapshots);
    }

    /// <summary>The mappings module's hook that writes report bridges.</summary>
    private ReportBridges ReportBridges()
    {
        var store = new BridgeStore(Bridges, BridgeVersions, BridgeMappings, Live(), _ids, _time);
        return new ReportBridges(Bridges, BridgeVersions, BridgeMappings, store, new BridgeVersionRemover(Bridges, BridgeVersions, BridgeMappings), Live(), _ids);
    }

    /// <summary>The snapshot loader.</summary>
    private ReportSnapshotLoader Loader()
    {
        return new ReportSnapshotLoader(Versions(), GraphReader(), Owned(), Roles, DatasetVersions, Live());
    }

    /// <summary>The reader of the report with its versions.</summary>
    private ReportVersionReader Versions()
    {
        return new ReportVersionReader(Reports, ReportVersions, Live());
    }

    /// <summary>The remover of a report version's content.</summary>
    private ReportVersionRemover VersionRemover()
    {
        return new ReportVersionRemover(
            Reports, ReportVersions, Profiles, Picks, SourceVariables, LeftOut, Sources, CalculationDimensions, Scopes, Scenarios, RowParameters, Lines, Columns,
            MemberOptions, Styles);
    }

    /// <summary>The diagnostics builder.</summary>
    private ReportDiagnosticsBuilder Diagnostics()
    {
        return new ReportDiagnosticsBuilder(
            Roles, DimensionKeys(), Variables, CalculatedMeasures, SnapshotDatasets(), new DatasetFieldValues(DatasetVersions, Catalog));
    }

    /// <summary>Every report rule.</summary>
    private ReportRules Rules()
    {
        IReportRule[] rules =
        [
            new ReportItemsResolveRule(), new CalculationGroupingResolveRule(), new ScenarioRolesDistinctRule(), new DefaultProfileEnabledRule(),
            new ReportDefaultsResolveRule(), new ColumnKeysDistinctRule(), new ReportMetricsResolveRule(), new ProfilesCompleteRule(),
            new ReportSourcesResolveRule(), new ReportFactorsApplyRule(), new ReportRowParametersResolveRule(), new ReportStylesResolveRule()
        ];
        return new ReportRules(rules, GraphReader());
    }

    /// <summary>The report graph reader.</summary>
    private ReportGraphReader GraphReader()
    {
        return new ReportGraphReader(
            Versions(), Owned(), Members, Columns, Lines, MemberOptions, Styles, RowParameters, Scopes, Scenarios, Profiles, Picks, Sources, SourceVariables, LeftOut,
            BridgeMappings, CalculationDimensions, ModelVersions, Variables, CalculatedMeasures, ParameterService(), Datasets, Roles, SnapshotDatasets(), DimensionKeys());
    }

    /// <summary>The reader of the versions the report owns.</summary>
    private OwnedVersions Owned()
    {
        return new OwnedVersions(DimensionVersions, BridgeVersions);
    }

    /// <summary>The reader of a snapshot's dataset versions.</summary>
    private SnapshotDatasets SnapshotDatasets()
    {
        return new SnapshotDatasets(Datasets, DatasetVersions, Fields, Bindings, TimeBindings, Measures);
    }

    /// <summary>The reader of dimensions' versions in a snapshot.</summary>
    private SnapshotDimensionReader DimensionKeys()
    {
        return new SnapshotDimensionReader(DimensionVersions, Levels, Members);
    }

    /// <summary>The semantic revision.</summary>
    private SemanticRevision Revision()
    {
        return new SemanticRevision(DatabaseInfo, _time);
    }

    /// <summary>The dimensions module's hook that creates and removes report dimensions.</summary>
    private ReportDimensions DimensionService()
    {
        var bands = new InMemoryTableGateway<RangeBandRecord>();
        var liveVersions = new LiveDimensionVersions(DimensionVersions, new DimensionVersionCloner(Levels, Members, bands, Closure, _ids), Live(), _ids, _time);
        var remover = new DimensionVersionRemover(Dimensions, Roles, DimensionVersions, Levels, Members, Closure, bands);
        return new ReportDimensions(Dimensions, Roles, Members, Closure, liveVersions, remover, _ids);
    }
}

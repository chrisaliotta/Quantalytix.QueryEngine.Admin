using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// The tables of one report and the report editor's services over them. Disabled report 60 reads model 40, whose published version
/// 41 has variable <c>balance</c>, and owns report dimension 500, whose one version 501 holds the rows TOTAL, a group, with LOANS
/// and CASH, data rows, under it. Its bridge source <c>gl</c> reads GL's <c>balance</c>, which ignores bridge factors, by
/// <c>as_of_date</c> and classifies GL's rows into account dimension 800, whose published version 801 holds the folder LENDING over
/// accounts 1001 and 1003, and account 1002; the source's many-to-many bridge 90 maps 1001 to LOANS and 1002 to CASH in its one
/// version 91. Profile <c>base</c>, the report's default, picks the source for balance, and column <c>amount</c> returns balance,
/// so the report passes every report rule. Release 900 is active and pins GL version 11; the active assumption set pins versions
/// 501, 801 and 91.
/// </summary>
internal sealed class ReportEditorFixture
{
    /// <summary>The report dimension's one version, which holds the rows.</summary>
    public const long StructureVersionId = 501;

    /// <summary>The analytical dimension of accounts, which the source's role belongs to.</summary>
    public const long AccountDimensionId = 800;

    /// <summary>The published version of the account dimension.</summary>
    public const long AccountVersionId = 801;

    /// <summary>The account role GL binds.</summary>
    public const long AccountRoleId = 85;

    /// <summary>The GL dataset.</summary>
    public const long GlDatasetId = 1;

    /// <summary>The GL version the active release pins.</summary>
    public const long GlVersionId = 11;

    /// <summary>The bridge source.</summary>
    public const long SourceId = 70;

    /// <summary>The source's bridge from accounts into the report dimension.</summary>
    public const long BridgeId = 90;

    /// <summary>The bridge's one version, which holds the item mappings.</summary>
    public const long BridgeVersionId = 91;

    /// <summary>The active assumption set.</summary>
    public const long ActiveSetId = 1;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 7;

    /// <summary>Allocates the IDs of new rows.</summary>
    private readonly SequentialIdGenerator _ids = new(5000);

    /// <summary>The clock.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The database info: release 900 is active.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 11,
        SemanticRevision = StartingRevision,
        ActiveReleaseId = ReleaseId,
        UpdatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The active release pins GL v11.</summary>
    public InMemoryTableGateway<ReleaseDatasetRecord> Manifest { get; } = new(Pin(GlDatasetId, GlVersionId));

    /// <summary>The disabled report, which defaults to profile base.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(ReportRows.Report() with { IsEnabled = false, DefaultProfileKey = "base" });

    /// <summary>The report dimension and the account dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(ReportRows.ReportDimensionId, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report },
        Dimension(AccountDimensionId, "account"));

    /// <summary>The published versions of both dimensions.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        DimensionRows.Version(StructureVersionId, ReportRows.ReportDimensionId, 1, PublicationStatuses.Published),
        DimensionRows.Version(AccountVersionId, AccountDimensionId, 1, PublicationStatuses.Published));

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

    /// <summary>The bridge's one version, published, which replicates each item into every row its rules name.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(
        BridgeRows.BridgeVersion(BridgeVersionId, BridgeId, 1, Cardinalities.ManyToMany, AllocationModes.Replicate, PublicationStatuses.Published));

    /// <summary>The item mappings: 1001 to LOANS and 1002 to CASH.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new(
        BridgeRows.Mapping(BridgeVersionId, "1001", "LOANS", 1m),
        BridgeRows.Mapping(BridgeVersionId, "1002", "CASH", 1m));

    /// <summary>The bridge source <c>gl</c>.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(ReportRows.Source(SourceId, "gl", GlDatasetId) with
    {
        MappingMethod = ReportMappingMethods.Bridge,
        FieldKey = null,
        RoleId = AccountRoleId,
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

    /// <summary>The variable scopes; none at first.</summary>
    public InMemoryTableGateway<ReportVariableScopeRecord> Scopes { get; } = new();

    /// <summary>The calculation dimensions; none.</summary>
    public InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions { get; } = new();

    /// <summary>Model 40's published version 41.</summary>
    public InMemoryTableGateway<CalculationModelVersionRecord> ModelVersions { get; } = new(ModelVersion());

    /// <summary>The balance variable.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance"));

    /// <summary>No calculated measures.</summary>
    public InMemoryTableGateway<CalculatedMeasureRecord> CalculatedMeasures { get; } = new();

    /// <summary>The GL dataset.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(Dataset(GlDatasetId, "gl"));

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

    /// <summary>The active assumption set.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> Sets { get; } = new(new AssumptionSetRecord
    {
        AssumptionSetId = ActiveSetId,
        AssumptionSetKey = "active",
        DisplayName = "Active",
        Status = AssumptionSetStatuses.Active,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The active set's pins of the report dimension's and the account dimension's versions.</summary>
    public InMemoryTableGateway<AssumptionSetDimensionRecord> DimensionPins { get; } = new(
        new AssumptionSetDimensionRecord { AssumptionSetId = ActiveSetId, DimensionId = ReportRows.ReportDimensionId, DimensionVersionId = StructureVersionId },
        new AssumptionSetDimensionRecord { AssumptionSetId = ActiveSetId, DimensionId = AccountDimensionId, DimensionVersionId = AccountVersionId });

    /// <summary>The active set's pin of the bridge's version.</summary>
    public InMemoryTableGateway<AssumptionSetBridgeRecord> BridgePins { get; } = new(
        new AssumptionSetBridgeRecord { AssumptionSetId = ActiveSetId, BridgeId = BridgeId, BridgeVersionId = BridgeVersionId });

    /// <summary>The route of a request to the report's source.</summary>
    public ContentKeys Keys { get; } = new(new FixedRequestRoute(new Dictionary<string, long> { ["reportId"] = ReportRows.ReportId, ["sourceId"] = SourceId }));

    /// <summary>The report service.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportService ReportService()
    {
        return new ReportService(Reports, DimensionService(), PublishedDimensions(), Workflow(), Owned(), Remover(), Pins(), Changes(), _ids);
    }

    /// <summary>The new report validator.</summary>
    /// <returns>The validator over these tables.</returns>
    public NewReportInputValidator NewReportValidator()
    {
        return new NewReportInputValidator(Reports, Dimensions, ModelVersions, PublishedDimensions());
    }

    /// <summary>The service that enables, disables and checks the report.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportStatusService Statuses()
    {
        return new ReportStatusService(Reports, GraphReader(), Rules());
    }

    /// <summary>The editor view reader.</summary>
    /// <returns>The reader over these tables.</returns>
    public ReportEditorReader Reader()
    {
        return new ReportEditorReader(
            Reports, Members, MemberOptions, Lines, Columns, Sources, SourceVariables, BridgeMappings, Datasets, Roles, Metrics(), Owned(), PublishedDimensions());
    }

    /// <summary>The row tree service.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportRowService Rows()
    {
        return new ReportRowService(
            Reports, Members, Lines, MemberOptions, Scopes, Columns, Sources, Mappings(), Owned(), new ClosureBuilder(Members, Closure), Changes(), _ids);
    }

    /// <summary>The row tree validator, for a request to report 60.</summary>
    /// <returns>The validator over these tables.</returns>
    public ReportRowsInputValidator RowsValidator()
    {
        return new ReportRowsInputValidator(Reports, Owned(), Members, Lines, Sources, BridgeMappings, new FormulaRowReferences(Metrics(), Columns), Keys);
    }

    /// <summary>The source service.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportSourceService SourceService()
    {
        return new ReportSourceService(Reports, Sources, Roles, BridgeService(), BridgeVersionService(), Mappings(), Owned(), Workflow(), Remover(), Changes(), _ids);
    }

    /// <summary>The new source validator, for a request to report 60.</summary>
    /// <returns>The validator over these tables.</returns>
    public NewReportSourceInputValidator NewSourceValidator()
    {
        return new NewReportSourceInputValidator(Reports, Sources, Bridges, Roles, ReleaseDatasets(), Keys);
    }

    /// <summary>The item service.</summary>
    /// <returns>The service over these tables.</returns>
    public SourceItemService Items()
    {
        return new SourceItemService(Sources, Mappings(), Owned(), Changes());
    }

    /// <summary>The item validator, for a request to the report's source.</summary>
    /// <returns>The validator over these tables.</returns>
    public SourceItemsInputValidator ItemsValidator()
    {
        return new SourceItemsInputValidator(Reports, Sources, Roles, Owned(), Members, PublishedDimensions(), Keys);
    }

    /// <summary>The coverage reader.</summary>
    /// <returns>The reader over these tables.</returns>
    public SourceCoverageReader Coverage()
    {
        return new SourceCoverageReader(Sources, Roles, BridgeMappings, Owned(), PublishedDimensions());
    }

    /// <summary>The column service.</summary>
    /// <returns>The service over these tables.</returns>
    public ReportColumnService ColumnService()
    {
        return new ReportColumnService(Columns, Changes(), _ids);
    }

    /// <summary>The bridge service.</summary>
    /// <returns>The service over these tables.</returns>
    public BridgeService BridgeService()
    {
        return new BridgeService(
            Bridges, BridgeVersions, Dimensions, Bindings, Sources, Reports, new BindingNames(Roles, Fields), BridgeVersionService(), Revision(), _ids);
    }

    /// <summary>Sets whether the report is enabled, as the status endpoint would once its rules pass.</summary>
    /// <param name="isEnabled">Whether the report is enabled.</param>
    /// <returns>A task that completes when the report is stored.</returns>
    public async Task SetEnabledAsync(bool isEnabled)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var report = await Reports.GetAsync(new { ReportRows.ReportId }, cancellationToken);
        await Reports.UpdateAsync(report with { IsEnabled = isEnabled }, cancellationToken);
    }

    /// <summary>The write gate.</summary>
    private ReportChanges Changes()
    {
        return new ReportChanges(Reports, GraphReader(), Rules());
    }

    /// <summary>Every report rule.</summary>
    private ReportRules Rules()
    {
        IReportRule[] rules =
        [
            new ReportItemsResolveRule(), new CalculationGroupingResolveRule(), new DefaultProfileEnabledRule(), new ReportDefaultsResolveRule(),
            new ColumnKeysDistinctRule(), new ReportMetricsResolveRule(), new ProfilesCompleteRule(), new ReportSourcesResolveRule(),
            new ReportFactorsApplyRule()
        ];
        return new ReportRules(rules, GraphReader());
    }

    /// <summary>The report graph reader.</summary>
    private ReportGraphReader GraphReader()
    {
        return new ReportGraphReader(
            Reports, Owned(), Members, Columns, Lines, MemberOptions, Scopes, Profiles, Picks, Sources, SourceVariables, BridgeMappings,
            CalculationDimensions, ModelVersions, Metrics(), Variables, CalculatedMeasures, Datasets, Roles, ReleaseDatasets(), PublishedDimensions());
    }

    /// <summary>The reader of the published version of the report's model.</summary>
    private ModelMetricsReader Metrics()
    {
        return new ModelMetricsReader(Reports, ModelVersions, Variables, CalculatedMeasures);
    }

    /// <summary>The reader of the versions the report owns.</summary>
    private OwnedVersions Owned()
    {
        return new OwnedVersions(DimensionVersions, BridgeVersions);
    }

    /// <summary>The reader of the active release's dataset versions.</summary>
    private ReleaseDatasets ReleaseDatasets()
    {
        return new ReleaseDatasets(DatabaseInfo, Manifest, Datasets, Fields, Bindings, TimeBindings, Measures);
    }

    /// <summary>The reader of dimensions' published versions.</summary>
    private PublishedDimensionReader PublishedDimensions()
    {
        return new PublishedDimensionReader(DimensionVersions, Levels, Members, new PublicationSet());
    }

    /// <summary>The report content remover.</summary>
    private ReportContentRemover Remover()
    {
        return new ReportContentRemover(Sources, SourceVariables, Profiles, Picks, CalculationDimensions, Scopes, Columns, Lines, MemberOptions, Owned(), BridgeService(), Pins());
    }

    /// <summary>The assumption set pins.</summary>
    private AssumptionPins Pins()
    {
        return new AssumptionPins(Sets, DimensionPins, new InMemoryTableGateway<AssumptionSetMappingRecord>(), BridgePins, new InMemoryTableGateway<AssumptionSetModelRecord>());
    }

    /// <summary>
    /// The version workflow over the dimension and bridge versions, without publication rules: the closure builder rebuilds a
    /// validated dimension version's closure, and the active set pins every published version.
    /// </summary>
    private VersionWorkflow Workflow()
    {
        var stores = new VersionStoreRegistry([new DimensionVersionStore(DimensionVersions), new BridgeVersionStore(BridgeVersions)]);
        IVersionLifecycleHandler[] handlers =
        [
            new ClosureBuilder(Members, Closure),
            new ActiveSetPinner(VersionKinds.DimensionVersion, stores, Pins()),
            new ActiveSetPinner(VersionKinds.BridgeVersion, stores, Pins())
        ];
        return new VersionWorkflow(stores, [], [], handlers, new PublicationSet(), Revision(), _time);
    }

    /// <summary>The dimension service, which creates and removes report dimensions.</summary>
    private DimensionService DimensionService()
    {
        var bands = new InMemoryTableGateway<RangeBandRecord>();
        var versions = new DimensionVersionService(
            DimensionVersions,
            new DimensionVersionCloner(Levels, Members, bands, _ids),
            new DimensionVersionRemover(DimensionVersions, Levels, Members, Closure, bands),
            _ids);
        var checks = Checks(bands);
        var members = new DimensionMemberService(Members, checks, new ClosureBuilder(Members, Closure), _ids);
        return new DimensionService(
            Dimensions,
            DimensionVersions,
            Roles,
            versions,
            new DimensionRoleService(Roles, checks, Revision(), _ids),
            members,
            new DimensionVersionRemover(DimensionVersions, Levels, Members, Closure, bands),
            checks,
            Revision(),
            _ids);
    }

    /// <summary>The delete checks over these tables, whose live consumers are read through an empty publication set.</summary>
    private DimensionDeleteChecks Checks(InMemoryTableGateway<RangeBandRecord> bands)
    {
        var datasetVersions = new InMemoryTableGateway<DatasetVersionRecord>();
        var mappingSets = new InMemoryTableGateway<MappingSetRecord>();
        var names = new BindingNames(Roles, Fields);
        var consumers = new DimensionConsumers(
            Datasets, datasetVersions, Bindings, Roles, mappingSets, new InMemoryTableGateway<MappingVersionRecord>(), new InMemoryTableGateway<DimensionMappingRecord>(),
            Bridges, BridgeVersions, BridgeMappings, Reports, CalculationDimensions, names);
        return new DimensionDeleteChecks(
            DimensionVersions, Members, bands, Roles, mappingSets, Bridges, Bindings, Datasets, datasetVersions, Reports, CalculationDimensions, Sources,
            new ReportNames(Reports), consumers);
    }

    /// <summary>The bridge version service.</summary>
    private BridgeVersionService BridgeVersionService()
    {
        return new BridgeVersionService(BridgeVersions, BridgeMappings, new BridgeVersionCloner(BridgeMappings, _ids), _ids);
    }

    /// <summary>The bridge mapping service.</summary>
    private BridgeMappingService Mappings()
    {
        return new BridgeMappingService(BridgeMappings, _ids);
    }

    /// <summary>The semantic revision.</summary>
    private SemanticRevision Revision()
    {
        return new SemanticRevision(DatabaseInfo, _time);
    }
}

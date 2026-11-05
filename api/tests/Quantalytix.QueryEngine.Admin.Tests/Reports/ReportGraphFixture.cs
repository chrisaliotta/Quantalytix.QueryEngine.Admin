using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetRows;
using static Quantalytix.QueryEngine.Admin.Tests.Support.CatalogRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// The tables the report rules read, seeded with one enabled report that resolves. The live snapshot holds GL version 11,
/// whose <c>gl_account</c> binds role <c>gl_account</c> of account dimension 800, whose <c>as_of_date</c> is a time binding and
/// whose <c>region</c> binds role <c>region</c> of region dimension 600. Report <c>balance_sheet</c> over dimension 500 reads model
/// 40, which has variable <c>balance</c> and calculated measure <c>yield</c>. The report returns <c>balance</c> in column
/// <c>amount</c>, scopes it to member <c>assets</c>, fills it from source <c>gl</c>, which reads GL's <c>balance</c> with the row
/// keys <c>gl_account</c> holds, picks that source in the profile <c>base</c> it defaults to, and breaks out by calculation
/// dimension <c>region</c> on the region role. Version 501 of dimension 500 is its one version, with <c>total</c> at the root and
/// <c>assets</c> under it; bridge 95 leads from accounts into dimension 500. The loans dataset exists but the snapshot holds no version of it. Tests
/// change rows to break one thing.
/// </summary>
internal sealed class ReportGraphFixture
{
    /// <summary>The GL dataset.</summary>
    public const long GlDatasetId = 1;

    /// <summary>The loans dataset.</summary>
    public const long LoansDatasetId = 2;

    /// <summary>GL's live version.</summary>
    public const long GlVersionId = 11;

    /// <summary>The loans version, which a test can add to the live snapshot.</summary>
    public const long LoansVersionId = 21;

    /// <summary>The GL source.</summary>
    public const long GlSourceId = 71;

    /// <summary>The account dimension GL's <c>gl_account</c> binding classifies into.</summary>
    public const long AccountDimensionId = 800;

    /// <summary>The bridge from accounts into the report's dimension.</summary>
    public const long AccountBridgeId = 95;

    /// <summary>The bridge's one version.</summary>
    public const long AccountBridgeVersionId = 96;

    /// <summary>The account role GL's <c>gl_account</c> binds, which a bridge source can start from.</summary>
    public const long AccountRoleId = 85;

    /// <summary>The region role GL's <c>region</c> binds, which the region calculation dimension reads.</summary>
    public const long RegionRoleId = 86;

    /// <summary>The database info.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 20,
        SemanticRevision = 1,
        UpdatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The live snapshot.</summary>
    public InMemoryTableGateway<SnapshotRecord> Snapshots { get; } = new(SnapshotRows.Live());

    /// <summary>The balance sheet report.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(Report());

    /// <summary>The report's live version, which defaults to the base profile.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(ReportRows.Version() with { DefaultProfileKey = "base" });

    /// <summary>The amount column, which returns the balance variable.</summary>
    public InMemoryTableGateway<ReportColumnRecord> Columns { get; } = new(Column(1, "amount", ReportValueKinds.Variable, "balance", 1));

    /// <summary>No overlay rows.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new();

    /// <summary>No member options.</summary>
    public InMemoryTableGateway<ReportMemberOptionRecord> MemberOptions { get; } = new();

    /// <summary>No styles.</summary>
    public InMemoryTableGateway<ReportStyleRecord> Styles { get; } = new();

    /// <summary>No row parameter values.</summary>
    public InMemoryTableGateway<ReportRowParameterRecord> RowParameters { get; } = new();

    /// <summary>The balance variable's scope, assets.</summary>
    public InMemoryTableGateway<ReportVariableScopeRecord> Scopes { get; } = new(Scope("balance", "assets"));

    /// <summary>No scenarios.</summary>
    public InMemoryTableGateway<ReportScenarioRecord> Scenarios { get; } = new();

    /// <summary>The base profile.</summary>
    public InMemoryTableGateway<SourceProfileRecord> Profiles { get; } = new(Profile(75, "base"));

    /// <summary>The base profile's pick of the GL source for the balance variable.</summary>
    public InMemoryTableGateway<SourceProfileVariableRecord> Picks { get; } = new(Pick(75, "balance", GlSourceId));

    /// <summary>The GL source.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(Source(GlSourceId, "gl", GlDatasetId));

    /// <summary>No left-out items.</summary>
    public InMemoryTableGateway<ReportSourceLeftOutRecord> LeftOut { get; } = new();

    /// <summary>The GL source's supply of the balance variable.</summary>
    public InMemoryTableGateway<ReportSourceVariableRecord> SourceVariables { get; } = new(SourceVariable(GlSourceId, "balance"));

    /// <summary>The region calculation dimension, on the region role.</summary>
    public InMemoryTableGateway<CalculationDimensionRecord> Dimensions { get; } = new(CalculationDimension(81, "region", RegionRoleId));

    /// <summary>The account and region roles.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(
        DimensionRows.Role(AccountRoleId, "gl_account", AccountDimensionId),
        DimensionRows.Role(RegionRoleId, "region", 600));

    /// <summary>Model 40.</summary>
    public InMemoryTableGateway<CalculationModelRecord> Models { get; } = new(Model());

    /// <summary>Model 40's live version.</summary>
    public InMemoryTableGateway<CalculationModelVersionRecord> ModelVersions { get; } = new(ModelRows.Version());

    /// <summary>The balance variable.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance"));

    /// <summary>The yield calculated measure.</summary>
    public InMemoryTableGateway<CalculatedMeasureRecord> CalculatedMeasures { get; } = new(Calculated(61, "yield", "[m.balance] / 2"));

    /// <summary>No attributions.</summary>
    public InMemoryTableGateway<MetricAttributionRecord> Attributions { get; } = new();

    /// <summary>No attribution drivers.</summary>
    public InMemoryTableGateway<MetricAttributionDriverRecord> AttributionDrivers { get; } = new();

    /// <summary>No model parameters.</summary>
    public InMemoryTableGateway<ModelParameterRecord> Parameters { get; } = new();

    /// <summary>The GL and loans datasets.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(Dataset(GlDatasetId, "gl"), Dataset(LoansDatasetId, "loans"));

    /// <summary>GL's live version.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(DatasetRows.Version(GlVersionId) with { DatasetId = GlDatasetId });

    /// <summary>GL's fields, and the loans fields a test reads once it holds loans; <c>memo</c> is neither filterable nor groupable.</summary>
    public InMemoryTableGateway<FieldRecord> Fields { get; } = new(
        ColumnField(1, GlVersionId, "gl_account", "gl_account"),
        ColumnField(2, GlVersionId, "as_of_date", "as_of_date", DataTypes.Date),
        ColumnField(3, GlVersionId, "region", "region"),
        ColumnField(4, GlVersionId, "memo", "memo") with { IsFilterable = false },
        ColumnField(5, LoansVersionId, "gl_account", "gl_account"),
        ColumnField(6, LoansVersionId, "as_of_date", "as_of_date", DataTypes.Date),
        ColumnField(7, LoansVersionId, "region", "region"));

    /// <summary>GL's bindings: the account role, the as-of date, and the region role.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new(
        Binding(31, GlVersionId, AccountRoleId, AccountDimensionId, BindingKinds.Direct) with { SourceFieldId = 1 },
        Binding(32, GlVersionId, null, 700, BindingKinds.Time) with { SourceFieldId = 2 },
        Binding(33, GlVersionId, RegionRoleId, 600, BindingKinds.Direct) with { SourceFieldId = 3 });

    /// <summary>The time extension of GL's as-of binding.</summary>
    public InMemoryTableGateway<TimeBindingRecord> TimeBindings { get; } = new(TimeBinding(32));

    /// <summary>GL's balance measure.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(Measure(41, GlVersionId, "balance", MeasureKinds.Aggregate));

    /// <summary>The bridge from accounts into the report's dimension.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(BridgeRows.Bridge(AccountBridgeId, AccountDimensionId, ReportDimensionId));

    /// <summary>The bridge's live version.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(
        BridgeRows.BridgeVersion(AccountBridgeVersionId, AccountBridgeId, Cardinalities.ManyToMany, AllocationModes.Replicate));

    /// <summary>The bridge's rules; none until a test makes GL a bridge source.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new();

    /// <summary>Version 501 of the report dimension, its live version.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(DimensionRows.Version(501, ReportDimensionId));

    /// <summary>No levels.</summary>
    public InMemoryTableGateway<DimensionLevelRecord> Levels { get; } = new();

    /// <summary>The report dimension's version 501: total at the root, with assets under it.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, 501, "total"),
        DimensionRows.Member(2, 501, "assets", parentId: 1));

    /// <summary>Adds loans v21 to the live snapshot, with a binding of its <c>gl_account</c> to the account role and a time binding of its as-of date.</summary>
    /// <returns>A task that completes when the rows are stored.</returns>
    public async Task HoldLoansAsync()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await DatasetVersions.InsertAsync(DatasetRows.Version(LoansVersionId) with { DatasetId = LoansDatasetId }, cancellationToken);
        await Bindings.InsertAsync(Binding(34, LoansVersionId, AccountRoleId, AccountDimensionId, BindingKinds.Direct) with { SourceFieldId = 5 }, cancellationToken);
        await Bindings.InsertAsync(Binding(35, LoansVersionId, null, 700, BindingKinds.Time) with { SourceFieldId = 6 }, cancellationToken);
        await TimeBindings.InsertAsync(TimeBinding(35), cancellationToken);
        await Measures.InsertAsync(Measure(42, LoansVersionId, "balance", MeasureKinds.Aggregate), cancellationToken);
    }

    /// <summary>The report graph reader over these tables.</summary>
    /// <returns>A reader for the report rules.</returns>
    public ReportGraphReader Reader()
    {
        return new ReportGraphReader(
            Versions(), Owned(), Members, Columns, Lines, MemberOptions, Styles, RowParameters, Scopes, Scenarios, Profiles, Picks, Sources, SourceVariables, LeftOut,
            BridgeMappings, Dimensions, ModelVersions, Variables, CalculatedMeasures, ParameterService(), Datasets, Roles, SnapshotDatasets(), DimensionKeys());
    }

    /// <summary>The reader of reports with their versions over these tables.</summary>
    /// <returns>The reader.</returns>
    public ReportVersionReader Versions()
    {
        return new ReportVersionReader(Reports, ReportVersions, new LiveSnapshot(Snapshots));
    }

    /// <summary>The parameters of model 40 over these tables.</summary>
    /// <returns>The service that reads and writes model parameters.</returns>
    public ModelParameterService ParameterService()
    {
        return new ModelParameterService(Parameters);
    }

    /// <summary>The attributions of model 40's metrics over these tables.</summary>
    /// <returns>The service that reads and writes attributions with their drivers.</returns>
    public MetricAttributionService AttributionService()
    {
        return new MetricAttributionService(Attributions, AttributionDrivers);
    }

    /// <summary>The reader of the versions the report owns.</summary>
    /// <returns>A reader of the report dimension's and the bridges' versions.</returns>
    public OwnedVersions Owned()
    {
        return new OwnedVersions(DimensionVersions, BridgeVersions);
    }

    /// <summary>The snapshot dataset reader over these tables.</summary>
    /// <returns>A reader of a snapshot's dataset versions.</returns>
    public SnapshotDatasets SnapshotDatasets()
    {
        return new SnapshotDatasets(Datasets, DatasetVersions, Fields, Bindings, TimeBindings, Measures);
    }

    /// <summary>The snapshot dimension reader over these tables.</summary>
    /// <returns>A reader of the dimensions' versions in a snapshot.</returns>
    public SnapshotDimensionReader DimensionKeys()
    {
        return new SnapshotDimensionReader(DimensionVersions, Levels, Members);
    }

    /// <summary>Every report rule, over these tables.</summary>
    /// <returns>The rules, as a write to an enabled report and enabling a report run them.</returns>
    public ReportRules Rules()
    {
        IReportRule[] rules =
        [
            new ReportItemsResolveRule(), new CalculationGroupingResolveRule(), new ScenarioRolesDistinctRule(), new DefaultProfileEnabledRule(),
            new ReportDefaultsResolveRule(), new ColumnKeysDistinctRule(), new ReportMetricsResolveRule(), new ProfilesCompleteRule(),
            new ReportSourcesResolveRule(), new ReportFactorsApplyRule(), new ReportRowParametersResolveRule(), new ReportStylesResolveRule()
        ];
        return new ReportRules(rules, Reader());
    }

    /// <summary>Checks the report's live version as it stands against one rule, in the scope of the live snapshot.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule's violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IReportRule rule)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var reader = Reader();
        var report = await reader.ReadAsync(await Versions().LiveAsync(ReportId, cancellationToken), cancellationToken);
        return [.. rule.Evaluate(report, await reader.ScopeAsync([report], SnapshotRows.LiveId, cancellationToken))];
    }

    /// <summary>Checks the enabled reports that read GL against one rule, as a commit to GL's live version does.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule's violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateGlReadersAsync(IReportRule rule)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var reader = Reader();
        var reports = await reader.EnabledReadingAsync(SnapshotRows.LiveId, GlDatasetId, cancellationToken);
        var scope = await reader.ScopeAsync(reports, SnapshotRows.LiveId, cancellationToken);
        return [.. reports.SelectMany(report => rule.Evaluate(report, scope))];
    }
}

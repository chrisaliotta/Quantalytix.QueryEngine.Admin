using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// The tables the report rules read, seeded with one enabled report that resolves. Release 900 is active and pins GL version 11,
/// whose <c>gl_account</c> binds role <c>gl_account</c> of account dimension 800, whose <c>as_of_date</c> is a time binding and
/// whose <c>region</c> binds role <c>region</c> of region dimension 600. Report <c>balance_sheet</c> over dimension 500 reads model
/// 40, which has variable <c>balance</c> and calculated measure <c>yield</c>. The report returns <c>balance</c> in column
/// <c>amount</c>, scopes it to member <c>assets</c>, fills it from source <c>gl</c>, which reads GL's <c>balance</c> with the row
/// keys <c>gl_account</c> holds, picks that source in the profile <c>base</c> it defaults to, and breaks out by calculation
/// dimension <c>region</c> on the region role. Version 501 of dimension 500 is its one version, with <c>total</c> at the root and
/// <c>assets</c> under it; bridge 95 leads from accounts into dimension 500. The loans dataset exists but is not pinned. Besides the
/// active assumption set, the tenant has captured set <c>close_2025</c>. Tests change rows to break one thing.
/// </summary>
internal sealed class ReportGraphFixture
{
    /// <summary>The GL dataset.</summary>
    public const long GlDatasetId = 1;

    /// <summary>The loans dataset.</summary>
    public const long LoansDatasetId = 2;

    /// <summary>The GL version the release pins.</summary>
    public const long GlVersionId = 11;

    /// <summary>The loans version, which a test can pin.</summary>
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

    /// <summary>The database info: release 900 is active.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 12,
        SemanticRevision = 1,
        ActiveReleaseId = ReleaseId,
        UpdatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The release pins GL v11.</summary>
    public InMemoryTableGateway<ReleaseDatasetRecord> Manifest { get; } = new(Pin(GlDatasetId, GlVersionId));

    /// <summary>The balance sheet report, which defaults to the base profile.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(Report() with { DefaultProfileKey = "base" });

    /// <summary>The amount column, which returns the balance variable.</summary>
    public InMemoryTableGateway<ReportColumnRecord> Columns { get; } = new(Column(1, "amount", ReportValueKinds.Variable, "balance", 1));

    /// <summary>No overlay rows.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new();

    /// <summary>No member options.</summary>
    public InMemoryTableGateway<ReportMemberOptionRecord> MemberOptions { get; } = new();

    /// <summary>The balance variable's scope, assets.</summary>
    public InMemoryTableGateway<ReportVariableScopeRecord> Scopes { get; } = new(Scope("balance", "assets"));

    /// <summary>The base profile.</summary>
    public InMemoryTableGateway<SourceProfileRecord> Profiles { get; } = new(Profile(75, "base"));

    /// <summary>The base profile's pick of the GL source for the balance variable.</summary>
    public InMemoryTableGateway<SourceProfileVariableRecord> Picks { get; } = new(Pick(75, "balance", GlSourceId));

    /// <summary>The GL source.</summary>
    public InMemoryTableGateway<ReportSourceRecord> Sources { get; } = new(Source(GlSourceId, "gl", GlDatasetId));

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

    /// <summary>The balance variable.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance"));

    /// <summary>The yield calculated measure.</summary>
    public InMemoryTableGateway<CalculatedMeasureRecord> CalculatedMeasures { get; } = new(Calculated(61, "yield", "[m.balance] / 2"));

    /// <summary>No attributions.</summary>
    public InMemoryTableGateway<MetricAttributionRecord> Attributions { get; } = new();

    /// <summary>No attribution drivers.</summary>
    public InMemoryTableGateway<MetricAttributionDriverRecord> AttributionDrivers { get; } = new();

    /// <summary>The active assumption set and the historical set <c>close_2025</c>.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> AssumptionSets { get; } = new(
        new AssumptionSetRecord
        {
            AssumptionSetId = 1,
            AssumptionSetKey = "active",
            DisplayName = "Active",
            Status = AssumptionSetStatuses.Active,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new AssumptionSetRecord
        {
            AssumptionSetId = 2,
            AssumptionSetKey = "close_2025",
            DisplayName = "2025 close",
            Status = AssumptionSetStatuses.Historical,
            ReleaseId = ReleaseId,
            CapturedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)
        });

    /// <summary>The GL and loans datasets.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(Dataset(GlDatasetId, "gl"), Dataset(LoansDatasetId, "loans"));

    /// <summary>GL's fields, and the loans fields a test reads once it pins loans; <c>memo</c> is neither filterable nor groupable.</summary>
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

    /// <summary>The bridge's one version, published.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(
        BridgeRows.BridgeVersion(AccountBridgeVersionId, AccountBridgeId, 1, Cardinalities.ManyToMany, AllocationModes.Replicate, PublicationStatuses.Published));

    /// <summary>The bridge's rules; none until a test makes GL a bridge source.</summary>
    public InMemoryTableGateway<BridgeMappingRecord> BridgeMappings { get; } = new();

    /// <summary>Version 501 of the report dimension, its one version, published.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(DimensionRows.Version(501, ReportDimensionId, 1, PublicationStatuses.Published));

    /// <summary>No levels.</summary>
    public InMemoryTableGateway<DimensionLevelRecord> Levels { get; } = new();

    /// <summary>The report dimension's version 501: total at the root, with assets under it.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, 501, "total"),
        DimensionRows.Member(2, 501, "assets", parentId: 1));

    /// <summary>Pins loans v21, with a binding of its <c>gl_account</c> to the account role and a time binding of its as-of date.</summary>
    /// <returns>A task that completes when the rows are stored.</returns>
    public async Task PinLoansAsync()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await Manifest.InsertAsync(Pin(LoansDatasetId, LoansVersionId), cancellationToken);
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
            Reports, Owned(), Members, Columns, Lines, MemberOptions, Scopes, Profiles, Picks, Sources, SourceVariables, BridgeMappings, Dimensions,
            Variables, CalculatedMeasures, Datasets, Roles, AssumptionSets, ReleaseDatasets(), PublishedDimensions());
    }

    /// <summary>The model gate over these tables.</summary>
    /// <returns>The checks a write to model 40 or its metrics goes through.</returns>
    public ModelChanges Changes()
    {
        return new ModelChanges(Models, Variables, CalculatedMeasures, AttributionService(), Reports, Reader());
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

    /// <summary>The release dataset reader over these tables.</summary>
    /// <returns>A reader of the pinned dataset versions.</returns>
    public ReleaseDatasets ReleaseDatasets()
    {
        return new ReleaseDatasets(DatabaseInfo, Manifest, Datasets, Fields, Bindings, TimeBindings, Measures);
    }

    /// <summary>The published dimension reader over these tables.</summary>
    /// <returns>A reader of the dimensions' published versions.</returns>
    public PublishedDimensionReader PublishedDimensions()
    {
        return new PublishedDimensionReader(DimensionVersions, Levels, Members, new PublicationSet());
    }

    /// <summary>Every report rule, over these tables.</summary>
    /// <returns>The rules, as a write to an enabled report and enabling a report run them.</returns>
    public ReportRules Rules()
    {
        IReportRule[] rules =
        [
            new ReportItemsResolveRule(), new CalculationGroupingResolveRule(), new ScenarioDimensionRule(), new DefaultProfileEnabledRule(),
            new ReportDefaultsResolveRule(), new ColumnKeysDistinctRule(), new ReportMetricsResolveRule(), new ProfilesCompleteRule(),
            new ReportSourcesResolveRule(), new ReportFactorsApplyRule()
        ];
        return new ReportRules(rules, Reader());
    }

    /// <summary>Checks the report as it stands against one rule, in the scope of the active release.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule's violations.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IReportRule rule)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var reader = Reader();
        var report = await reader.ReadAsync(await Reports.GetAsync(new { ReportId }, cancellationToken), cancellationToken);
        return [.. rule.Evaluate(report, await reader.ScopeAsync([report], null, cancellationToken))];
    }

    /// <summary>Checks the enabled reports a release being validated serves against one rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="releaseId">The release being validated.</param>
    /// <returns>The rule's violations.</returns>
    public Task<IReadOnlyList<RuleViolation>> EvaluateReleaseAsync(IReportRule rule, long releaseId)
    {
        return new ReportConsumerRule(rule, Reader()).EvaluateAsync(releaseId, Xunit.TestContext.Current.CancellationToken);
    }
}

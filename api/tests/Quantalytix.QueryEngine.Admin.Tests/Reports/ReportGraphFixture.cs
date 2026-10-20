using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// The tables the report rules read, seeded with one published report that resolves. Release 900 is active and pins GL
/// version 11, whose <c>gl_account</c> is a direct binding into the report-item dimension 500, whose <c>as_of_date</c> is a
/// time binding and whose <c>region</c> is a binding into region dimension 600. Report <c>balance_sheet</c> v1 reads model
/// 40, whose published version 41 has variable <c>balance</c>, which targets member <c>assets</c> and reads GL's
/// <c>balance</c>, calculated measure <c>yield</c>, the enabled profile <c>base</c> the report defaults to, and calculation
/// dimension <c>region</c> routed to GL's <c>region</c>. The report's column <c>amount</c> returns <c>balance</c> and its
/// member line shows <c>assets</c>. Version 501 of the report-item dimension is published, with <c>total</c> at the root and
/// <c>assets</c> under it. Model version 42 is an empty draft, and the loans dataset exists but is not pinned. Tests change
/// rows to break one thing.
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

    /// <summary>The draft version of model 40, which tests fill and validate as the version being published.</summary>
    public const long DraftModelVersionId = 42;

    /// <summary>The database info: release 900 is active.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "bank",
        MetadataSchemaVersion = 8,
        SemanticRevision = 1,
        ActiveReleaseId = ReleaseId,
        UpdatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The release pins GL v11.</summary>
    public InMemoryTableGateway<ReleaseDatasetRecord> Manifest { get; } = new(Pin(GlDatasetId, GlVersionId));

    /// <summary>The balance sheet report.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(new ReportRecord
    {
        ReportId = 60,
        ReportKey = "balance_sheet",
        DisplayName = "Balance sheet",
        ReportType = "financial",
        IsEnabled = true
    });

    /// <summary>The published v1 of the report, which defaults to the base profile.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(ReportVersion(ReportVersionId, PublicationStatuses.Published) with { DefaultProfileKey = "base" });

    /// <summary>The amount column, which returns the balance variable.</summary>
    public InMemoryTableGateway<ReportColumnRecord> Columns { get; } = new(Column(1, "amount", ReportValueKinds.Variable, "balance", 1));

    /// <summary>The assets member line.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new(Line(11, "assets", ReportLineKinds.Member, null, 1) with { ReportMemberKey = "assets" });

    /// <summary>Model 40's published version 41 and draft version 42.</summary>
    public InMemoryTableGateway<CalculationModelVersionRecord> ModelVersions { get; } = new(ModelVersion(), ModelVersion(DraftModelVersionId, PublicationStatuses.Draft));

    /// <summary>The balance variable, targeting assets.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance", "assets"));

    /// <summary>The yield calculated measure.</summary>
    public InMemoryTableGateway<CalculatedMeasureRecord> CalculatedMeasures { get; } = new(Calculated(61, "yield", "[m.balance] / 2"));

    /// <summary>The base profile.</summary>
    public InMemoryTableGateway<SourceProfileRecord> Profiles { get; } = new(Profile(75, "base"));

    /// <summary>The GL source of the balance variable.</summary>
    public InMemoryTableGateway<VariableSourceRecord> Sources { get; } = new(Source(71, 51, "gl_balance", GlDatasetId));

    /// <summary>The region calculation dimension.</summary>
    public InMemoryTableGateway<CalculationDimensionRecord> Dimensions { get; } = new(CalculationDimension(81, "region", 600));

    /// <summary>GL's default route of the region dimension.</summary>
    public InMemoryTableGateway<CalculationRouteRecord> Routes { get; } = new(Route(91, 81, GlDatasetId, "region"));

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

    /// <summary>GL's bindings: report items, the as-of date, and region.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new(
        Binding(31, GlVersionId, "GL account", 500, BindingKinds.Direct) with { SourceFieldId = 1 },
        Binding(32, GlVersionId, "As of", 700, BindingKinds.Time) with { SourceFieldId = 2 },
        Binding(33, GlVersionId, "Region", 600, BindingKinds.Direct) with { SourceFieldId = 3 });

    /// <summary>The time extension of GL's as-of binding.</summary>
    public InMemoryTableGateway<TimeBindingRecord> TimeBindings { get; } = new(TimeBinding(32));

    /// <summary>GL's balance measure.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(Measure(41, GlVersionId, "balance", MeasureKinds.Aggregate));

    /// <summary>No bridge versions.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new();

    /// <summary>Version 501 of the report-item dimension, published.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(DimensionRows.Version(501, 500, 1, PublicationStatuses.Published));

    /// <summary>Report-item version 501: total at the root, with assets under it.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, 501, "total"),
        DimensionRows.Member(2, 501, "assets", parentId: 1));

    /// <summary>Pins loans v21, with a binding of its <c>gl_account</c> into the report items and a time binding of its as-of date.</summary>
    /// <returns>A task that completes when the rows are stored.</returns>
    public async Task PinLoansAsync()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await Manifest.InsertAsync(Pin(LoansDatasetId, LoansVersionId), cancellationToken);
        await Bindings.InsertAsync(Binding(34, LoansVersionId, "Loan GL account", 500, BindingKinds.Direct) with { SourceFieldId = 5 }, cancellationToken);
        await Bindings.InsertAsync(Binding(35, LoansVersionId, "Loan as of", 700, BindingKinds.Time) with { SourceFieldId = 6 }, cancellationToken);
        await TimeBindings.InsertAsync(TimeBinding(35), cancellationToken);
        await Measures.InsertAsync(Measure(42, LoansVersionId, "balance", MeasureKinds.Aggregate), cancellationToken);
    }

    /// <summary>The report graph reader over these tables.</summary>
    /// <returns>A reader for the report rules.</returns>
    public ReportGraphReader Reader()
    {
        return new ReportGraphReader(
            DatabaseInfo, Manifest, Reports, ReportVersions, Columns, Lines, ModelVersions,
            new ModelMetricsReader(ReportVersions, ModelVersions, Variables, CalculatedMeasures),
            Variables, CalculatedMeasures, Profiles, Sources, Dimensions, Routes, Datasets, Fields, Bindings, TimeBindings, Measures, BridgeVersions);
    }

    /// <summary>The published dimension reader over these tables.</summary>
    /// <returns>A reader of the report-item dimension's published version.</returns>
    public PublishedDimensionReader PublishedDimensions()
    {
        return new PublishedDimensionReader(DimensionVersions, new InMemoryTableGateway<DimensionLevelRecord>(), Members);
    }

    /// <summary>Evaluates a rule against a version.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="versionId">The release, report version or model version the rule's kind validates.</param>
    /// <returns>The rule's violations.</returns>
    public static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IPublicationRule rule, long versionId)
    {
        return rule.EvaluateAsync(versionId, Xunit.TestContext.Current.CancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// The tables the report release rules read, seeded with one pinned report that resolves. Release 900 pins GL version 11, whose
/// <c>gl_account</c> is a direct binding into the report-item dimension 500 pinned to version 501, whose <c>as_of_date</c> is a
/// time binding and whose <c>region</c> is a binding into region dimension 600. Report <c>balance_sheet</c> v1 reads model
/// version 41: variable <c>balance</c> targets member <c>assets</c> and reads GL's <c>balance</c>, and calculation dimension
/// <c>region</c> routes to GL's <c>region</c>. The loans dataset exists but is not pinned. Tests change rows to break one thing.
/// </summary>
internal sealed class ReleaseReportFixture
{
    /// <summary>The GL dataset.</summary>
    public const long GlDatasetId = 1;

    /// <summary>The loans dataset.</summary>
    public const long LoansDatasetId = 2;

    /// <summary>The GL version the release pins.</summary>
    public const long GlVersionId = 11;

    /// <summary>The loans version, which a test can pin.</summary>
    public const long LoansVersionId = 21;

    /// <summary>The release pins report 60 v1.</summary>
    public InMemoryTableGateway<ReleaseReportRecord> Pins { get; } = new(new ReleaseReportRecord { ReleaseId = ReleaseId, ReportId = 60, ReportVersionId = ReportVersionId });

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

    /// <summary>The published v1 of the report.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(ReportVersion(ReportVersionId, status: PublicationStatuses.Published));

    /// <summary>The assets member line.</summary>
    public InMemoryTableGateway<ReportLineRecord> Lines { get; } = new(Line(11, "assets", ReportLineKinds.Member, null, 1) with { ReportMemberKey = "assets" });

    /// <summary>The balance variable, targeting assets.</summary>
    public InMemoryTableGateway<VariableRecord> Variables { get; } = new(Variable(51, "balance", "assets"));

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

    /// <summary>GL's bindings: report items pinned to v501, the as-of date, and region.</summary>
    public InMemoryTableGateway<DimensionBindingRecord> Bindings { get; } = new(
        Binding(31, GlVersionId, "GL account", 500, BindingKinds.Direct) with { SourceFieldId = 1, PinnedDimensionVersionId = 501 },
        Binding(32, GlVersionId, "As of", 700, BindingKinds.Time) with { SourceFieldId = 2 },
        Binding(33, GlVersionId, "Region", 600, BindingKinds.Direct) with { SourceFieldId = 3 });

    /// <summary>The time extension of GL's as-of binding.</summary>
    public InMemoryTableGateway<TimeBindingRecord> TimeBindings { get; } = new(TimeBinding(32));

    /// <summary>GL's balance measure.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(Measure(41, GlVersionId, "balance", MeasureKinds.Aggregate));

    /// <summary>No bridge versions.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new();

    /// <summary>Report-item version 501: total at the root, with assets under it.</summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        DimensionRows.Member(1, 501, "total"),
        DimensionRows.Member(2, 501, "assets", parentId: 1));

    /// <summary>Pins loans v21 with a binding of its <c>gl_account</c> into the report items, pinned to the given version.</summary>
    /// <param name="reportItemVersionId">The report-item version the loans binding pins.</param>
    /// <returns>A task that completes when the rows are stored.</returns>
    public async Task PinLoansAsync(long reportItemVersionId)
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await Manifest.InsertAsync(Pin(LoansDatasetId, LoansVersionId), cancellationToken);
        await Bindings.InsertAsync(Binding(34, LoansVersionId, "Loan GL account", 500, BindingKinds.Direct) with { SourceFieldId = 5, PinnedDimensionVersionId = reportItemVersionId }, cancellationToken);
        await Bindings.InsertAsync(Binding(35, LoansVersionId, "Loan as of", 700, BindingKinds.Time) with { SourceFieldId = 6 }, cancellationToken);
        await TimeBindings.InsertAsync(TimeBinding(35), cancellationToken);
        await Measures.InsertAsync(Measure(42, LoansVersionId, "balance", MeasureKinds.Aggregate), cancellationToken);
    }

    /// <summary>The reader over these tables.</summary>
    /// <returns>A reader for the release rules.</returns>
    public ReleaseReportReader Reader()
    {
        return new ReleaseReportReader(
            Pins, Manifest, Reports, ReportVersions, Lines, Variables, Sources, Dimensions, Routes, Datasets, Fields, Bindings, TimeBindings, Measures, BridgeVersions);
    }

    /// <summary>Evaluates a release rule against release 900.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule's violations.</returns>
    public static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IPublicationRule rule)
    {
        return rule.EvaluateAsync(ReleaseId, Xunit.TestContext.Current.CancellationToken);
    }
}

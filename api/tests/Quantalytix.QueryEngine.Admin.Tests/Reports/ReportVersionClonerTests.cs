using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that cloning a report version onto another model version follows metrics by key: columns and formula lines move
/// to the new model's metrics with the same keys, a column whose metric the new model lacks is dropped with the formula line
/// that places its value there, and that line's children move up to its parent. Authors clone a report to adopt a new model
/// version, so the clone must keep every line it can and name what it dropped.
/// </summary>
public sealed class ReportVersionClonerTests
{
    /// <summary>The new model version, which has balance and yield but no margin.</summary>
    private const long TargetModelVersionId = 42;

    /// <summary>
    /// Version 61 shows amount, yield and margin columns; its assets section holds the margin formula line, under which the
    /// loans member line sits.
    /// </summary>
    [Fact]
    public async Task MetricsFollowTheirKeys_AndDroppedLinesHandTheirChildrenUp()
    {
        var versions = new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId));
        var columns = new InMemoryTableGateway<ReportColumnRecord>(
            Column(1, "amount", ReportValueKinds.Variable, 51, 1),
            Column(2, "yield", ReportValueKinds.Calculated, 91, 2),
            Column(3, "margin", ReportValueKinds.Calculated, 92, 3));
        var lines = new InMemoryTableGateway<ReportLineRecord>(
            Line(11, "assets", ReportLineKinds.Section, null, 1),
            Line(12, "margin_line", ReportLineKinds.Formula, 11, 1) with { CalculatedMeasureId = 92, ValueColumnId = 3 },
            Line(13, "loans", ReportLineKinds.Member, 12, 1) with { ReportMemberKey = "loans" });
        var metrics = new ModelMetricsReader(
            versions,
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance"), Variable(52, "balance") with { ModelVersionId = TargetModelVersionId }),
            new InMemoryTableGateway<CalculatedMeasureRecord>(
                Calculated(91, "yield", "[m.balance] / 2"),
                Calculated(92, "margin", "[m.balance] / 4"),
                Calculated(93, "yield", "[m.balance] / 2") with { ModelVersionId = TargetModelVersionId }));
        var target = ReportVersion(62, TargetModelVersionId);

        var created = await new ReportVersionCloner(versions, columns, lines, metrics, new SequentialIdGenerator(1000))
            .CloneAsync(ReportVersionId, target, TestContext.Current.CancellationToken);

        Assert.Equal(["margin"], created.DroppedColumns);
        Assert.Equal(["margin_line"], created.DroppedLines);
        Assert.Equal(
            [("amount", (long?)52, (long?)null), ("yield", null, 93)],
            columns.All.Where(column => column.ReportVersionId == 62).OrderBy(column => column.SortOrder)
                .Select(column => (column.ColumnKey, column.VariableId, column.CalculatedMeasureId)));
        var cloned = lines.All.Where(line => line.ReportVersionId == 62).ToDictionary(line => line.LineKey);
        Assert.Equal(["assets", "loans"], cloned.Keys.Order());
        Assert.Equal(cloned["assets"].ReportLineId, cloned["loans"].ParentReportLineId);
    }
}

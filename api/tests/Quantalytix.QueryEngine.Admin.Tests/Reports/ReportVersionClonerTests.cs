using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that cloning a report version copies its columns and lines under new IDs, with their metric, measure and member
/// keys as they are, and points each copied line at the copies of its parent and value column. Columns and lines name
/// metrics and members by key, so the copy reads whichever model and dimension versions an assumption set pins; a copy that
/// pointed back at the source's rows would let edits to the draft change the published version.
/// </summary>
public sealed class ReportVersionClonerTests
{
    /// <summary>
    /// Version 61 shows amount and yield columns; its assets section holds the yield formula line, under which the loans
    /// member line sits.
    /// </summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task ColumnsAndLines_AreCopiedWithParentsAndValueColumnsRemapped()
    {
        var columns = new InMemoryTableGateway<ReportColumnRecord>(
            Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
            Column(2, "yield", ReportValueKinds.Calculated, "yield", 2));
        var lines = new InMemoryTableGateway<ReportLineRecord>(
            Line(11, "assets", ReportLineKinds.Section, null, 1),
            Line(12, "yield_line", ReportLineKinds.Formula, 11, 1) with { CalculatedMeasureKey = "yield", ValueColumnId = 2 },
            Line(13, "loans", ReportLineKinds.Member, 12, 1) with { ReportMemberKey = "loans" });

        await new ReportVersionCloner(columns, lines, new SequentialIdGenerator(1000))
            .CloneAsync(ReportVersionId, ReportVersion(62), TestContext.Current.CancellationToken);

        var clonedColumns = columns.All.Where(column => column.ReportVersionId == 62).ToDictionary(column => column.ColumnKey);
        var clonedLines = lines.All.Where(line => line.ReportVersionId == 62).ToDictionary(line => line.LineKey);
        Assert.Equal([("amount", "balance"), ("yield", "yield")], clonedColumns.Values.OrderBy(column => column.SortOrder).Select(column => (column.ColumnKey, column.MetricKey)));
        Assert.DoesNotContain(clonedColumns.Values, column => column.ReportColumnId is 1 or 2);
        Assert.Equal(clonedLines["assets"].ReportLineId, clonedLines["yield_line"].ParentReportLineId);
        Assert.Equal(clonedLines["yield_line"].ReportLineId, clonedLines["loans"].ParentReportLineId);
        Assert.Equal(clonedColumns["yield"].ReportColumnId, clonedLines["yield_line"].ValueColumnId);
        Assert.Equal(("yield", "loans"), (clonedLines["yield_line"].CalculatedMeasureKey, clonedLines["loans"].ReportMemberKey));
    }
}

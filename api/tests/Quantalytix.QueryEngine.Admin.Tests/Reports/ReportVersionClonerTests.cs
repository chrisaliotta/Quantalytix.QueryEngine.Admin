using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that cloning a report version copies all its content under new IDs: keys of metrics, members and datasets stay as they
/// are, while a pick follows the copy of its profile and source, a route the copies of its calculation dimension and overridden
/// source, and a formula row the copy of its value column. The copy reads whichever model and dimension versions an assumption set
/// pins; a copy that pointed back at the source's rows would let edits to the draft change the published version.
/// </summary>
public sealed class ReportVersionClonerTests
{
    /// <summary>
    /// Version 61 targets balance at assets, fills it from a GL source the base profile picks, routes region to GL with an override
    /// for that source, shows amount and yield, places the nim formula row in yield, and makes total a header.
    /// </summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Content_IsCopiedWithReferencesBetweenCopiesRemapped()
    {
        var targets = new InMemoryTableGateway<ReportVariableTargetRecord>(Target("balance", "assets"));
        var sources = new InMemoryTableGateway<VariableSourceRecord>(Source(71, "balance", "gl_balance", 1));
        var profiles = new InMemoryTableGateway<SourceProfileRecord>(Profile(75, "base"));
        var picks = new InMemoryTableGateway<SourceProfileVariableRecord>(Pick(75, "balance", 71));
        var dimensions = new InMemoryTableGateway<CalculationDimensionRecord>(CalculationDimension(81, "region", 600));
        var routes = new InMemoryTableGateway<CalculationRouteRecord>(Route(91, 81, 1, "region"), Route(92, 81, 1, "region_code") with { VariableSourceId = 71 });
        var columns = new InMemoryTableGateway<ReportColumnRecord>(
            Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
            Column(2, "yield", ReportValueKinds.Calculated, "yield", 2));
        var lines = new InMemoryTableGateway<ReportLineRecord>(Line(11, "nim", ReportLineKinds.Formula, null, 9) with { CalculatedMeasureKey = "yield", ValueColumnId = 2 });
        var memberOptions = new InMemoryTableGateway<ReportMemberOptionRecord>(MemberOption("total"));

        await new ReportVersionCloner(targets, sources, profiles, picks, dimensions, routes, columns, lines, memberOptions, new SequentialIdGenerator(1000))
            .CloneAsync(ReportVersionId, 62, TestContext.Current.CancellationToken);

        var source = Assert.Single(sources.All, row => row.ReportVersionId == 62);
        var profile = Assert.Single(profiles.All, row => row.ReportVersionId == 62);
        var pick = Assert.Single(picks.All, row => row.SourceProfileId == profile.SourceProfileId);
        var dimension = Assert.Single(dimensions.All, row => row.ReportVersionId == 62);
        var target = Assert.Single(targets.All, row => row.ReportVersionId == 62);
        var clonedColumns = columns.All.Where(column => column.ReportVersionId == 62).ToDictionary(column => column.ColumnKey);
        Assert.NotEqual(71, source.VariableSourceId);
        Assert.Equal(("balance", "gl_balance"), (source.VariableKey, source.SourceKey));
        Assert.Equal(("balance", source.VariableSourceId), (pick.VariableKey, pick.VariableSourceId));
        Assert.Equal(
            [(dimension.CalculationDimensionId, (long?)null), (dimension.CalculationDimensionId, source.VariableSourceId)],
            routes.All.Where(route => route.ReportVersionId == 62).OrderBy(route => route.FieldKey).Select(route => (route.CalculationDimensionId, route.VariableSourceId)));
        Assert.Equal([("amount", "balance"), ("yield", "yield")], clonedColumns.Values.OrderBy(column => column.SortOrder).Select(column => (column.ColumnKey, column.MetricKey)));
        Assert.Equal(clonedColumns["yield"].ReportColumnId, Assert.Single(lines.All, line => line.ReportVersionId == 62).ValueColumnId);
        Assert.Equal(("balance", "assets"), (target.VariableKey, target.TargetMemberKey));
        Assert.Equal("total", Assert.Single(memberOptions.All, option => option.ReportVersionId == 62).MemberKey);
    }
}

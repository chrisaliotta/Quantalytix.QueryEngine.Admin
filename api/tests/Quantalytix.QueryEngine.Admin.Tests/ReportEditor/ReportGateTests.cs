using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms the gate that keeps every report the engine sees runnable. Enabling a report checks it against every report rule and
/// leaves it disabled on any violation, so the engine never reads a report it cannot run. Once a report is enabled, the engine reads
/// each write at once, so a write that would break a rule is refused and nothing is written; the same write to a disabled report is
/// taken, because enabling checks the result.
/// </summary>
public sealed class ReportGateTests
{
    /// <summary>A report without columns stays disabled, naming the rule; with its column back it is enabled.</summary>
    /// <returns>A task that completes when the status changes are checked.</returns>
    [Fact]
    public async Task EnablingABreakingReport_LeavesItDisabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.Columns.DeleteAsync(new { ReportColumnId = 1L }, cancellationToken);

        var violation = Assert.Single(await fixture.Statuses().SetAsync(ReportRows.ReportId, new ReportStatusInput { IsEnabled = true }, cancellationToken));

        Assert.Equal(ColumnKeysDistinctRule.Code, violation.Rule);
        Assert.False(Assert.Single(fixture.Reports.All).IsEnabled);

        await fixture.Columns.InsertAsync(ReportRows.Column(1, "amount", ReportValueKinds.Variable, "balance", 1), cancellationToken);
        Assert.Empty(await fixture.Statuses().SetAsync(ReportRows.ReportId, new ReportStatusInput { IsEnabled = true }, cancellationToken));
        Assert.True(Assert.Single(fixture.Reports.All).IsEnabled);
    }

    /// <summary>Removing every column of the enabled report is refused and keeps the column; removing them while it is disabled is taken.</summary>
    /// <returns>A task that completes when the writes are checked.</returns>
    [Fact]
    public async Task BreakingWriteToAnEnabledReport_WritesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.SetEnabledAsync(true);

        var violation = Assert.Single(await fixture.ColumnService().ReplaceAsync(ReportRows.ReportId, new ReportColumnsInput { Columns = [] }, cancellationToken));

        Assert.Equal(ColumnKeysDistinctRule.Code, violation.Rule);
        Assert.Equal(["amount"], fixture.Columns.All.Select(column => column.ColumnKey));

        await fixture.SetEnabledAsync(false);
        Assert.Empty(await fixture.ColumnService().ReplaceAsync(ReportRows.ReportId, new ReportColumnsInput { Columns = [] }, cancellationToken));
        Assert.Empty(fixture.Columns.All);
    }
}

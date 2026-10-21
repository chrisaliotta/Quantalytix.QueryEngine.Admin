using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms which report rows block deleting a model's metrics: enabled reports resolve metric keys in the model's published
/// version, so a variable one of their columns returns, source variables supply, scopes or reads through a calculated measure, and a
/// calculated measure a column returns or a formula row evaluates, cannot be deleted from that version; the same key in a draft model
/// version blocks nothing, because no report reads drafts, and a disabled report blocks nothing, because the engine does not read it.
/// </summary>
public sealed class ReportUsesTests
{
    /// <summary>The draft version of model 40.</summary>
    private const long DraftModelVersionId = 42;

    /// <summary>The enabled balance sheet returns balance and yield, places yield in the nim row, fills balance from GL and scopes it.</summary>
    private readonly InMemoryTableGateway<ReportRecord> _reports = new(Report());

    /// <summary>The enabled report's variable is read in every way a report reads one, including through yield's formula.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task VariableOfThePublishedVersion_IsUsedByEveryReportRowThatReadsIt()
    {
        var uses = await Uses().VariableUsesAsync(Variable(51, "balance"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["column 'amount' of report 'balance_sheet'",
             "column 'yield' of report 'balance_sheet', through 'yield'",
             "row 'nim' of report 'balance_sheet', through 'yield'",
             "source 'gl' of report 'balance_sheet'",
             "the variable scope of report 'balance_sheet'"],
            uses);
    }

    /// <summary>The published version's measure is named by the enabled report's column and formula row; the draft's variable by nothing.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task OnlyThePublishedVersionsMetrics_AreUsed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            ["column 'yield' of report 'balance_sheet'", "row 'nim' of report 'balance_sheet'"],
            await Uses().MeasureUsesAsync(Calculated(61, "yield", "[m.balance] / 2"), cancellationToken));
        Assert.Empty(await Uses().VariableUsesAsync(Variable(52, "balance") with { ModelVersionId = DraftModelVersionId }, cancellationToken));
    }

    /// <summary>A disabled report's rows use nothing.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task DisabledReport_UsesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _reports.UpdateAsync(Report() with { IsEnabled = false }, cancellationToken);

        Assert.Empty(await Uses().VariableUsesAsync(Variable(51, "balance"), cancellationToken));
    }

    /// <summary>The report uses over model 40's published v1 and draft v2, and the balance sheet.</summary>
    private ReportUses Uses()
    {
        return new ReportUses(
            _reports,
            new InMemoryTableGateway<ReportColumnRecord>(
                Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
                Column(2, "yield", ReportValueKinds.Calculated, "yield", 2)),
            new InMemoryTableGateway<ReportLineRecord>(Line(11, "nim", ReportLineKinds.Formula, null, 1) with { CalculatedMeasureKey = "yield", ValueColumnId = 2 }),
            new InMemoryTableGateway<ReportSourceRecord>(Source(71, "gl", 1)),
            new InMemoryTableGateway<ReportSourceVariableRecord>(SourceVariable(71, "balance")),
            new InMemoryTableGateway<ReportVariableScopeRecord>(Scope("balance", "assets")),
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance")),
            new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(61, "yield", "[m.balance] / 2")),
            new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion(), ModelVersion(DraftModelVersionId, PublicationStatuses.Draft)));
    }
}

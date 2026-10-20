using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms which report rows block deleting a model's metrics: published report versions resolve metric keys in the model's
/// published version, so a variable one of their columns returns, sources supply, scopes or reads through a calculated measure, and
/// a calculated measure a column returns or a formula row evaluates, cannot be deleted from that version; the same key in a draft
/// model version blocks nothing, because no report reads drafts.
/// </summary>
public sealed class ReportUsesTests
{
    /// <summary>The draft version of model 40.</summary>
    private const long DraftModelVersionId = 42;

    /// <summary>The published balance sheet returns balance and yield, places yield in the nim row, fills balance from GL and scopes it.</summary>
    private readonly ReportUses _uses = Uses();

    /// <summary>The published version's variable is read in every way a report reads one, including through yield's formula.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task VariableOfThePublishedVersion_IsUsedByEveryReportRowThatReadsIt()
    {
        var uses = await _uses.VariableUsesAsync(Variable(51, "balance"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["column 'amount' of 'balance_sheet' v1",
             "column 'yield' of 'balance_sheet' v1, through 'yield'",
             "row 'nim' of 'balance_sheet' v1, through 'yield'",
             "source 'gl_balance' of 'balance_sheet' v1",
             "the variable scope of 'balance_sheet' v1"],
            uses);
    }

    /// <summary>The published version's measure is named by the live report's column and formula row; the draft's variable by nothing.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task OnlyThePublishedVersionsMetrics_AreUsed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            ["column 'yield' of 'balance_sheet' v1", "row 'nim' of 'balance_sheet' v1"],
            await _uses.MeasureUsesAsync(Calculated(61, "yield", "[m.balance] / 2"), cancellationToken));
        Assert.Empty(await _uses.VariableUsesAsync(Variable(52, "balance") with { ModelVersionId = DraftModelVersionId }, cancellationToken));
    }

    /// <summary>The report uses over model 40's published v1 and draft v2, and the published balance sheet.</summary>
    private static ReportUses Uses()
    {
        var versions = new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId, PublicationStatuses.Published) with { DefaultProfileKey = "base" });
        return new ReportUses(
            new ReportVersionNames(new InMemoryTableGateway<ReportRecord>(Report()), versions),
            versions,
            new InMemoryTableGateway<ReportColumnRecord>(
                Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
                Column(2, "yield", ReportValueKinds.Calculated, "yield", 2)),
            new InMemoryTableGateway<ReportLineRecord>(Line(11, "nim", ReportLineKinds.Formula, null, 1) with { CalculatedMeasureKey = "yield", ValueColumnId = 2 }),
            new InMemoryTableGateway<VariableSourceRecord>(Source(71, "balance", "gl_balance", 1)),
            new InMemoryTableGateway<ReportVariableScopeRecord>(Scope("balance", "assets")),
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance")),
            new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(61, "yield", "[m.balance] / 2")),
            new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion(), ModelVersion(DraftModelVersionId, PublicationStatuses.Draft)));
    }
}

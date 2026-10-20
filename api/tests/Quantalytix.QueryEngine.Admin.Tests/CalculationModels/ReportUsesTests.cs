using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms which report rows block deleting a model's content: published report versions resolve metric and profile keys in
/// the model's published version, so a key one of their columns, formula lines or defaults names cannot be deleted from that
/// version; the same key in a draft model version blocks nothing, because no report reads drafts.
/// </summary>
public sealed class ReportUsesTests
{
    /// <summary>The draft version of model 40.</summary>
    private const long DraftModelVersionId = 42;

    /// <summary>The report uses over model 40's published v1 and draft v2, and the published balance sheet that returns balance and yield.</summary>
    private readonly ReportUses _uses = new(
        new InMemoryTableGateway<ReportRecord>(new ReportRecord { ReportId = 60, ReportKey = "balance_sheet", DisplayName = "Balance sheet", ReportType = "financial", IsEnabled = true }),
        new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId, PublicationStatuses.Published) with { DefaultProfileKey = "base" }),
        new InMemoryTableGateway<ReportColumnRecord>(Column(1, "amount", ReportValueKinds.Variable, "balance", 1)),
        new InMemoryTableGateway<ReportLineRecord>(Line(11, "loan_yield", ReportLineKinds.Formula, null, 1) with { CalculatedMeasureKey = "yield", ValueColumnId = 1 }),
        new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion(), ModelVersion(DraftModelVersionId, PublicationStatuses.Draft)));

    /// <summary>The published version's variable, measure and profile are named by the live report; the draft's are not.</summary>
    /// <returns>A task that completes when the uses are checked.</returns>
    [Fact]
    public async Task OnlyThePublishedVersionsContent_IsUsed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(["column 'amount' of 'balance_sheet' v1"], await _uses.VariableColumnsAsync(Variable(51, "balance"), cancellationToken));
        Assert.Equal(["line 'loan_yield' of 'balance_sheet' v1"], await _uses.MeasureUsesAsync(Calculated(61, "yield", "[m.balance] / 2"), cancellationToken));
        Assert.Equal(["'balance_sheet' v1"], await _uses.ProfileDefaultsAsync(Profile(75, "base"), cancellationToken));
        Assert.Empty(await _uses.VariableColumnsAsync(Variable(52, "balance") with { ModelVersionId = DraftModelVersionId }, cancellationToken));
    }
}

using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms the model manager's commands and reads (blueprint 15): a new model is created disabled and empty on the Gregorian
/// calendar, a taken key is a conflict; a model a report reads cannot be deleted (C8), its delete check naming each report with
/// <c>referenced_by_report</c>, and a model no report reads is deleted with its content; the list and the document name the
/// reports that read a model.
/// </summary>
public sealed class ModelCommandsTests
{
    /// <summary>The tables and the module.</summary>
    private readonly ModelFixture _fixture = new();

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A new model is disabled, has no metrics and follows the Gregorian calendar.</summary>
    /// <returns>A task that completes when the model is checked.</returns>
    [Fact]
    public async Task Create_StoresADisabledEmptyModel()
    {
        var outcome = await _fixture.Commands().CreateAsync(Input("bank.cost_model"), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal((ModelFixture.FirstNewId, false, ModelCalendars.Gregorian), (outcome.Value.Id, outcome.Value.IsEnabled, outcome.Value.CalendarKey));
        var document = await _fixture.Queries().DocumentAsync(outcome.Value.Id, Token);
        Assert.Empty(document.Variables);
        Assert.Empty(document.UsedBy);
    }

    /// <summary>A key another model has is refused as a conflict, writing nothing.</summary>
    /// <returns>A task that completes when the refusal is checked.</returns>
    [Fact]
    public async Task CreateWithATakenKey_IsAConflict()
    {
        var outcome = await _fixture.Commands().CreateAsync(Input("margin"), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Single(_fixture.Tables.Models.All);
    }

    /// <summary>A model a report reads, even a disabled one, is blocked by that report, and nothing is deleted.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeleteOfAModelAReportReads_IsBlocked()
    {
        await _fixture.DisableReportAsync();

        var check = await _fixture.Queries().DeleteCheckAsync(ModelRows.CalculationModelId, Token);
        var refusal = await _fixture.Commands().DeleteAsync(ModelRows.CalculationModelId, Token);

        Assert.Equal([DeleteBlockerCodes.ReferencedByReport], check.Reasons.Select(reason => reason.Code));
        Assert.Equal(RefusalKind.DeleteBlocked, refusal!.Kind);
        Assert.Single(_fixture.Tables.Models.All);
    }

    /// <summary>A model no report reads is deleted with its variables and calculated measures.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeleteOfAModelNoReportReads_DeletesItsContent()
    {
        await _fixture.Tables.Reports.DeleteAsync(new { ReportId = Reports.ReportRows.ReportId }, Token);

        Assert.True((await _fixture.Queries().DeleteCheckAsync(ModelRows.CalculationModelId, Token)).Allowed);
        Assert.Null(await _fixture.Commands().DeleteAsync(ModelRows.CalculationModelId, Token));
        Assert.Empty(_fixture.Tables.Models.All);
        Assert.Empty(_fixture.Tables.Variables.All);
        Assert.Empty(_fixture.Tables.CalculatedMeasures.All);
    }

    /// <summary>The list counts each model's metrics and names the reports that read it.</summary>
    /// <returns>A task that completes when the list is checked.</returns>
    [Fact]
    public async Task List_CountsMetricsAndNamesReaders()
    {
        var item = Assert.Single(await _fixture.Queries().ListAsync(Token));

        Assert.Equal((1, 1), (item.VariableCount, item.MeasureCount));
        Assert.Equal(["balance_sheet"], item.UsedBy.Select(use => use.ReportKey));
    }

    /// <summary>A model's create input.</summary>
    private static CalcModelCreateInput Input(string key)
    {
        return new CalcModelCreateInput
        {
            Key = key,
            DisplayName = "Cost",
            CurrencyCode = "USD",
            FormulaLanguage = FormulaLanguageVersions.Version12,
            AttributionMethod = AttributionMethods.Sequential
        };
    }
}

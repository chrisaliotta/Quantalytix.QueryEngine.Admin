using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the column list check: the keys <c>line</c> and <c>period</c>, which name the report table's own columns, are
/// rejected, as is a repeated key; a column must return a metric of the value kind it declares; and a column that a formula
/// row places its value in cannot be removed. The replace deletes the columns a list leaves out, so a row would otherwise
/// be left pointing at a deleted column.
/// </summary>
public sealed class ReportColumnsInputValidatorTests
{
    /// <summary>The stored columns of report 60: amount returns the balance variable, yield the yield measure.</summary>
    private readonly InMemoryTableGateway<ReportColumnRecord> _columns = new(
        Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
        Column(2, "yield", ReportValueKinds.Calculated, "yield", 2));

    /// <summary>The formula row that places the yield measure in the yield column.</summary>
    private readonly InMemoryTableGateway<ReportLineRecord> _lines = new(
        Line(11, "loan_yield", ReportLineKinds.Formula, null, 1) with { CalculatedMeasureKey = "yield", ValueColumnId = 2 });

    /// <summary>A reserved key fails on its column, and a repeated key fails the list once.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ReservedAndRepeatedKeys_Fail()
    {
        var result = await ValidateAsync(
            Input(1, "amount", ReportValueKinds.Variable, "balance"),
            Input(2, "yield", ReportValueKinds.Calculated, "yield"),
            Input(null, "line", ReportValueKinds.Variable, "balance"),
            Input(null, "amount", ReportValueKinds.Variable, "balance"));

        result.ShouldHaveValidationErrorFor("Columns[2].ColumnKey").WithErrorMessage("'line' names a report table's own column; choose another key.");
        result.ShouldHaveValidationErrorFor(input => input.Columns).WithErrorMessage("'Columns' must give each column a different key.");
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>A calculated column that names the balance variable fails, because no calculated measure has that key.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task MetricOfAnotherKind_Fails()
    {
        var result = await ValidateAsync(
            Input(1, "amount", ReportValueKinds.Calculated, "balance"),
            Input(2, "yield", ReportValueKinds.Calculated, "yield"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Column 'amount' returns 'balance', which is no calculated metric of the published version of the report's model.", error.ErrorMessage);
    }

    /// <summary>Leaving out the yield column fails while the formula row still places its value there.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RemovingAColumnARowUses_Fails()
    {
        var result = await ValidateAsync(Input(1, "amount", ReportValueKinds.Variable, "balance"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Formula row 'loan_yield' places its value in column 'yield', so the column must stay.", error.ErrorMessage);
    }

    /// <summary>A visible column input.</summary>
    private static ReportColumnInput Input(long? columnId, string columnKey, string valueKind, string metricKey)
    {
        return new ReportColumnInput { ReportColumnId = columnId, ColumnKey = columnKey, DisplayName = columnKey, ValueKind = valueKind, MetricKey = metricKey, IsVisible = true };
    }

    /// <summary>Validates a column list for report 60 over model 40, whose published version is 41.</summary>
    private Task<TestValidationResult<ReportColumnsInput>> ValidateAsync(params ReportColumnInput[] columns)
    {
        var validator = new ReportColumnsInputValidator(
            new ModelMetricsReader(
                new InMemoryTableGateway<ReportRecord>(Report()),
                new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion()),
                new InMemoryTableGateway<VariableRecord>(Variable(51, "balance")),
                new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(91, "yield", "[m.balance] / 2"))),
            _columns,
            _lines,
            new ContentKeys(new FixedRequestRoute(new Dictionary<string, long> { ["reportId"] = ReportId })));
        return validator.TestValidateAsync(new ReportColumnsInput { Columns = columns }, cancellationToken: TestContext.Current.CancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>report_row_parameters_resolve</c>. The engine hands each row's parameter values, or its nearest ancestor's, to the
/// model's formulas without checking them, so every value must sit on a row of the report, be for a parameter the model declares,
/// and hold the parameter's type, within its allowed values. Model 40 declares <c>dayCount</c>, limited to the conventions, and
/// <c>scale</c>, a number; report <c>balance_sheet</c> has member rows <c>total</c> and <c>assets</c>.
/// </summary>
public sealed class ReportRowParametersResolveRuleTests
{
    /// <summary>The seeded tables, with model 40's parameters.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>A text value among the allowed ones on a member row and a number on an overlay row pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TypedValuesOnRows_Pass()
    {
        await DeclareAsync();
        await _fixture.Lines.InsertAsync(Line(21, "NOTE", ReportLineKinds.Section, "total", 0), TestContext.Current.CancellationToken);
        await SetAsync(Text("assets", "dayCount", DayCountConventions.Actual360), Number("NOTE", "scale", 1000m));

        Assert.Empty(await _fixture.EvaluateAsync(new ReportRowParametersResolveRule()));
    }

    /// <summary>
    /// A value on a row the report lacks, of a parameter the model lacks, of the other type, or outside the allowed values is each
    /// reported.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ValuesOutsideTheirRowsOrParameters_AreReported()
    {
        await DeclareAsync();
        await SetAsync(
            Text("gone", "dayCount", DayCountConventions.Actual360),
            Text("total", "basis", "a"),
            Text("total", "scale", "1000"),
            Number("assets", "dayCount", 360m),
            Text("assets", "scale", "x") with { NumberValue = 1m },
            Text("total", "dayCount", "monthly"));

        Assert.Equal(
            ["Report 'balance_sheet' row 'assets' sets parameter 'dayCount', which is a text parameter, so its value must be text.",
             "Report 'balance_sheet' row 'assets' sets parameter 'scale', which is a number parameter, so its value must be a number.",
             "Report 'balance_sheet' sets 'dayCount' on 'gone', which is no row of the report.",
             "Report 'balance_sheet' row 'total' sets 'basis', which is no parameter of the report's model.",
             "Report 'balance_sheet' row 'total' sets parameter 'dayCount', which takes only 'actual_actual', 'actual_365', 'actual_360', 'thirty_360_us', 'thirty_e_360', not 'monthly'.",
             "Report 'balance_sheet' row 'total' sets parameter 'scale', which is a number parameter, so its value must be a number."],
            (await _fixture.EvaluateAsync(new ReportRowParametersResolveRule())).Select(violation => violation.Message));
    }

    /// <summary>Declares model 40's day count and scale parameters.</summary>
    private async Task DeclareAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Parameters.InsertManyAsync([ParameterRow(DayCount()), ParameterRow(NumberParameter("scale", 1m))], cancellationToken);
    }

    /// <summary>Stores row values of report 60.</summary>
    private Task SetAsync(params IReadOnlyCollection<ReportRowParameterRecord> values)
    {
        return _fixture.RowParameters.InsertManyAsync(values, TestContext.Current.CancellationToken);
    }

    /// <summary>A row's text value of a parameter.</summary>
    private static ReportRowParameterRecord Text(string rowKey, string parameterKey, string text)
    {
        return new ReportRowParameterRecord { ReportId = ReportId, RowKey = rowKey, ParameterKey = parameterKey, TextValue = text };
    }

    /// <summary>A row's number value of a parameter.</summary>
    private static ReportRowParameterRecord Number(string rowKey, string parameterKey, decimal number)
    {
        return new ReportRowParameterRecord { ReportId = ReportId, RowKey = rowKey, ParameterKey = parameterKey, NumberValue = number };
    }
}

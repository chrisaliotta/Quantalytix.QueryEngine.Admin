using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the line tree check: a list in pre-order passes; a line whose parent comes later, and a repeated key, fail; and a
/// formula line must evaluate a calculated measure of the report's model version into one of the version's columns. The
/// replace deletes the lines a list leaves out, and the engine walks the stored tree in pre-order, so a malformed list must
/// fail before anything is written.
/// </summary>
public sealed class ReportLinesInputValidatorTests
{
    /// <summary>The validator under test, for report version 61 over model version 41, whose columns are amount and yield.</summary>
    private readonly ReportLinesInputValidator _validator = new(
        new ModelMetricsReader(
            new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId)),
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance")),
            new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(91, "yield", "[m.balance] / 2"))),
        new InMemoryTableGateway<ReportColumnRecord>(
            Column(1, "amount", ReportValueKinds.Variable, 51, 1),
            Column(2, "yield", ReportValueKinds.Calculated, 91, 2)),
        new InMemoryTableGateway<ReportLineRecord>(),
        new FixedRequestRoute(new Dictionary<string, long> { ["versionId"] = ReportVersionId }));

    /// <summary>A section with a member line and a formula line under it passes.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task PreOrderTree_Passes()
    {
        var result = await ValidateAsync(Section("assets"), Member("loans", "assets"), Formula("loan_yield", "assets", "yield", "yield"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A child listed before its parent fails, and so does a key listed twice.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ParentAfterChildAndRepeatedKey_Fail()
    {
        var result = await ValidateAsync(Member("loans", "assets"), Section("assets"), Section("assets"));

        Assert.Equal(
            ["Line 'loans' has parent 'assets', which must come earlier in the list.", "Line key 'assets' appears more than once."],
            result.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A formula line that names a variable instead of a calculated measure, and a column the version lacks, fails twice.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task FormulaLineOutsideTheModel_Fails()
    {
        var result = await ValidateAsync(Formula("loan_yield", null, "balance", "margin"));

        Assert.Equal(
            ["Formula line 'loan_yield' evaluates 'balance', which is no calculated measure of the report's model version.",
             "Formula line 'loan_yield' places its value in 'margin', which is no column of this report version."],
            result.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A section line.</summary>
    private static ReportLineInput Section(string lineKey, string? parentLineKey = null)
    {
        return new ReportLineInput { LineKey = lineKey, DisplayName = lineKey, LineKind = ReportLineKinds.Section, ParentLineKey = parentLineKey, IsVisible = true };
    }

    /// <summary>A member line that shows the member with the line's key.</summary>
    private static ReportLineInput Member(string lineKey, string? parentLineKey)
    {
        return Section(lineKey, parentLineKey) with { LineKind = ReportLineKinds.Member, ReportMemberKey = lineKey };
    }

    /// <summary>A formula line.</summary>
    private static ReportLineInput Formula(string lineKey, string? parentLineKey, string measureKey, string columnKey)
    {
        return Section(lineKey, parentLineKey) with { LineKind = ReportLineKinds.Formula, CalculatedMeasureKey = measureKey, ValueColumnKey = columnKey };
    }

    /// <summary>Validates a line list.</summary>
    private Task<TestValidationResult<ReportLinesInput>> ValidateAsync(params ReportLineInput[] lines)
    {
        return _validator.TestValidateAsync(new ReportLinesInput { Lines = lines }, cancellationToken: TestContext.Current.CancellationToken);
    }
}

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
/// Confirms the overlay row check: rows under members or under earlier overlay rows pass; a row whose overlay parent comes later, and
/// a repeated key, fail; and a formula row must evaluate a calculated measure of the published version of the report's model into
/// one of the version's columns. The replace deletes the rows a list leaves out, so a malformed list must fail before anything is
/// written.
/// </summary>
public sealed class ReportLinesInputValidatorTests
{
    /// <summary>The validator under test, for report version 61 over model 40, whose published version is 41; the columns are amount and yield.</summary>
    private readonly ReportLinesInputValidator _validator = new(
        new ModelMetricsReader(
            new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId)),
            new InMemoryTableGateway<CalculationModelVersionRecord>(ModelVersion()),
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance")),
            new InMemoryTableGateway<CalculatedMeasureRecord>(Calculated(91, "yield", "[m.balance] / 2"))),
        new InMemoryTableGateway<ReportColumnRecord>(
            Column(1, "amount", ReportValueKinds.Variable, "balance", 1),
            Column(2, "yield", ReportValueKinds.Calculated, "yield", 2)),
        new InMemoryTableGateway<ReportLineRecord>(),
        new FixedRequestRoute(new Dictionary<string, long> { ["versionId"] = ReportVersionId }));

    /// <summary>A section under the assets member with a formula row under it, and a top-level formula row, pass.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RowsUnderMembersAndEarlierRows_Pass()
    {
        var result = await ValidateAsync(Section("notes", "assets"), Formula("loan_yield", "notes", "yield", "yield"), Formula("nim", null, "yield", "yield"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A row listed before the overlay row it sits under fails, and so does a key listed twice.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task OverlayParentAfterChildAndRepeatedKey_Fail()
    {
        var result = await ValidateAsync(Section("detail", "notes"), Section("notes", "assets"), Section("notes", "assets"));

        Assert.Equal(
            ["Row 'detail' sits under overlay row 'notes', which must come earlier in the list.", "Row key 'notes' appears more than once."],
            result.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A formula row that names a variable instead of a calculated measure, and a column the version lacks, fails twice.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task FormulaRowOutsideTheModel_Fails()
    {
        var result = await ValidateAsync(Formula("loan_yield", null, "balance", "margin"));

        Assert.Equal(
            ["Formula row 'loan_yield' evaluates 'balance', which is no calculated measure of the published version of the report's model.",
             "Formula row 'loan_yield' places its value in 'margin', which is no column of this report version."],
            result.Errors.Select(error => error.ErrorMessage));
    }

    /// <summary>A section row.</summary>
    private static ReportLineInput Section(string lineKey, string? parentKey)
    {
        return new ReportLineInput { LineKey = lineKey, DisplayName = lineKey, LineKind = ReportLineKinds.Section, ParentKey = parentKey, SortOrder = 0, IsVisible = true };
    }

    /// <summary>A formula row.</summary>
    private static ReportLineInput Formula(string lineKey, string? parentKey, string measureKey, string columnKey)
    {
        return Section(lineKey, parentKey) with { LineKind = ReportLineKinds.Formula, CalculatedMeasureKey = measureKey, ValueColumnKey = columnKey };
    }

    /// <summary>Validates an overlay row list.</summary>
    private Task<TestValidationResult<ReportLinesInput>> ValidateAsync(params ReportLineInput[] lines)
    {
        return _validator.TestValidateAsync(new ReportLinesInput { Lines = lines }, cancellationToken: TestContext.Current.CancellationToken);
    }
}

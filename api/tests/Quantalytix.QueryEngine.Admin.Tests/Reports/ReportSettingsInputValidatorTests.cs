using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the shape of a report's executable JSON when its settings are saved, because the engine trusts it: the settings' layout
/// says how requests may filter, order and page the report, their default comparisons are what a request compares the run with when
/// it names none, and the default filter may read request parameters, which every request then supplies, but not the context that
/// calculated measures read for each period.
/// </summary>
public sealed class ReportSettingsInputValidatorTests
{
    /// <summary>The validator for a request to report 60, which has no profiles.</summary>
    private readonly ReportSettingsInputValidator _validator =
        new(new InMemoryTableGateway<SourceProfileRecord>(), new ContentKeys(new FixedRequestRoute(new Dictionary<string, long> { ["reportId"] = ReportId })));

    /// <summary>A layout is one the engine knows; any other would leave a run's result stage undefined.</summary>
    /// <param name="layout">The settings' layout.</param>
    /// <param name="valid">Whether the settings may be saved.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData(ReportLayouts.Statement, true)]
    [InlineData(ReportLayouts.List, true)]
    [InlineData("pivot", false)]
    public async Task SettingsLayout_IsStatementOrList(string layout, bool valid)
    {
        var result = await ValidateAsync(Input() with { DefaultSettingsJson = $$"""{"periodicity":"month","layout":"{{layout}}"}""" });

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(input => input.DefaultSettingsJson).WithErrorMessage("'layout' must be one of statement, list.");
    }

    /// <summary>A default filter compares calculation dimensions with a parameter every request supplies, but not with @periodDays or
    /// @yearDays, which calculated measures read for each period.</summary>
    /// <param name="parameter">The parameter the filter compares.</param>
    /// <param name="valid">Whether the settings may be saved.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData("branch", true)]
    [InlineData("periodDays", false)]
    public async Task DefaultFilter_MayCompareRequestParameters(string parameter, bool valid)
    {
        var filter = $$$"""{"operator":"eq","left":{"type":"dimension","id":"branch"},"right":{"type":"parameter","id":"{{{parameter}}}"}}""";

        var result = await ValidateAsync(Input() with { DefaultFilterJson = filter });

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor(input => input.DefaultFilterJson);
    }

    /// <summary>A default profile the report does not have fails, because requests that name no profile would run under nothing.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task DefaultProfileOfAnotherReport_Fails()
    {
        var result = await ValidateAsync(Input() with { DefaultProfileKey = "base" });

        result.ShouldHaveValidationErrorFor(input => input.DefaultProfileKey).WithErrorMessage("'base' is no profile of this report.");
    }

    /// <summary>
    /// The plan's default comparisons, a budget, the latest forecast, forecast against budget through <c>base</c>, and the prior year,
    /// pass as stored, because the engine reads them as a contract 2.1 request's comparisons.
    /// </summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task DefaultComparisonsInTheContractShape_Pass()
    {
        const string comparisons = """
            [
              { "key": "bud", "profileKey": "plan", "scenario": "BUD_FY26_V2", "transforms": ["comparison", "change", "ratio", "drivers"] },
              { "key": "fc", "profileKey": "plan", "scenario": { "latest": "FC_FY2026" }, "transforms": ["comparison", "change"] },
              { "key": "fcVsBud", "base": "fc", "profileKey": "plan", "scenario": "BUD_FY26_V2", "transforms": ["change"] },
              { "key": "py", "period": { "type": "shift", "length": 1, "unit": "year" }, "assumptionSet": "close_2025", "transforms": ["change", "percentChange", "contribution"] },
              { "key": "pme", "period": { "type": "periodEnd", "periodicity": "month", "offset": -1 }, "transforms": ["change"] },
              { "key": "fy25", "period": { "type": "range", "start": "2025-01-01", "endExclusive": "2026-01-01" }, "transforms": ["share"] }
            ]
            """;

        var result = await ValidateAsync(Input() with { DefaultSettingsJson = $$"""{"layout":"statement","comparisons":{{comparisons}}}""" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Each shape the engine would refuse or misread fails at the JSON path of the value inside the settings, so the settings sheet
    /// shows it on the comparison field that caused it: a key a column name cannot carry, a repeated key, a base that is not another
    /// comparison, transforms that are unknown or repeated, a period that keeps a member of another type or breaks its bounds, a
    /// malformed scenario, and more comparisons than a run makes, which the list itself carries.
    /// </summary>
    /// <param name="comparisons">The settings' comparisons.</param>
    /// <param name="failures">The failures expected, each as its key and message.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData("""[{"key":"bud.v2","transforms":["change"]}]""",
        new[] { "DefaultSettingsJson.comparisons[0].key: Comparison 1's key must start with a letter, hold only letters, digits and '_', and not be 'totals'." })]
    [InlineData("""[{"key":"py","transforms":["change"]},{"key":"py","base":"py","transforms":["ratio"]}]""",
        new[]
        {
            "DefaultSettingsJson.comparisons[0].key: Comparison 1's key 'py' is the key of another comparison too.",
            "DefaultSettingsJson.comparisons[1].key: Comparison 2's key 'py' is the key of another comparison too.",
            "DefaultSettingsJson.comparisons[1].base: Comparison 2's base must be the key of another comparison."
        })]
    [InlineData("""[{"key":"py","transforms":["delta"]},{"key":"pm","transforms":["change","change"]},{"key":"pq","transforms":[]}]""",
        new[]
        {
            "DefaultSettingsJson.comparisons[0].transforms: Comparison 1 must list one or more transforms of comparison, change, percentChange, ratio, share, drivers, contribution.",
            "DefaultSettingsJson.comparisons[1].transforms: Comparison 2 must name each transform once.",
            "DefaultSettingsJson.comparisons[2].transforms: Comparison 3 must list one or more transforms of comparison, change, percentChange, ratio, share, drivers, contribution."
        })]
    [InlineData("""[{"key":"mom","period":{"type":"shift","length":0,"unit":"week","offset":-1},"transforms":["change"]}]""",
        new[]
        {
            "DefaultSettingsJson.comparisons[0].period.offset: Comparison 1's 'shift' period does not read 'offset'.",
            "DefaultSettingsJson.comparisons[0].period.length: Comparison 1's shift length must be a whole number of 1 or more.",
            "DefaultSettingsJson.comparisons[0].period.unit: Comparison 1's unit must be one of day, month, quarter, year."
        })]
    [InlineData("""[{"key":"pme","period":{"type":"periodEnd","periodicity":"month","offset":1},"transforms":["change"]},{"key":"fy","period":{"type":"range","start":"2026-02-01","endExclusive":"2026-01-01"},"transforms":["change"]}]""",
        new[]
        {
            "DefaultSettingsJson.comparisons[0].period.offset: Comparison 1's offset must be a whole number of 0 or less, counted back from the period that holds the run period.",
            "DefaultSettingsJson.comparisons[1].period.endExclusive: Comparison 2's range must end after it starts."
        })]
    [InlineData("""[{"key":"fc","scenario":{"latest":"FC_FY2026","kind":"forecast"},"transforms":["change"]}]""",
        new[] { "DefaultSettingsJson.comparisons[0].scenario: Comparison 1's scenario must be a member key, or { \"latest\": \"<folder key>\" } for the latest version under a folder." })]
    [InlineData("""[{"key":"a","transforms":["change"]},{"key":"b","transforms":["change"]},{"key":"c","transforms":["change"]},{"key":"d","transforms":["change"]},{"key":"e","transforms":["change"]},{"key":"f","transforms":["change"]},{"key":"g","transforms":["change"]}]""",
        new[] { "DefaultSettingsJson.comparisons: There are 7 comparisons; a run makes at most 6." })]
    public async Task MalformedComparisons_FailAtTheirPaths(string comparisons, string[] failures)
    {
        var result = await ValidateAsync(Input() with { DefaultSettingsJson = $$"""{"comparisons":{{comparisons}}}""" });

        Assert.Equal(failures, result.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}"));
    }

    private static ReportSettingsInput Input() => new() { SubtotalPosition = SubtotalPositions.Above };

    private Task<TestValidationResult<ReportSettingsInput>> ValidateAsync(ReportSettingsInput input) =>
        _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
}

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
/// says how requests may filter, order and page the report, and the default filter may read request parameters, which every request
/// then supplies, but not the context that calculated measures read for each period.
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

    private static ReportSettingsInput Input() => new() { SubtotalPosition = SubtotalPositions.Above };

    private Task<TestValidationResult<ReportSettingsInput>> ValidateAsync(ReportSettingsInput input) =>
        _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
}

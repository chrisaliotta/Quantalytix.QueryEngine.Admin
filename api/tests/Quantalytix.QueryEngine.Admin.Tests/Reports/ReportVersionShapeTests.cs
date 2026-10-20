using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the shape of a report version's executable JSON when it is saved, because the engine trusts it: the settings' layout
/// says how requests may filter, order and page the report, and the default filter may read request parameters, which every request
/// then supplies, but not the context that calculated measures read for each period.
/// </summary>
public sealed class ReportVersionShapeTests
{
    private readonly ReportVersionInputValidator _validator =
        new(new InMemoryTableGateway<SourceProfileRecord>(), new FixedRequestRoute(new Dictionary<string, long> { ["versionId"] = 61 }));

    /// <summary>A layout is one the engine knows; any other would leave a run's result stage undefined.</summary>
    /// <param name="layout">The settings' layout.</param>
    /// <param name="valid">Whether the version may be saved.</param>
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
    /// <param name="valid">Whether the version may be saved.</param>
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

    private static ReportVersionInput Input() => new() { SubtotalPosition = SubtotalPositions.Above };

    private Task<TestValidationResult<ReportVersionInput>> ValidateAsync(ReportVersionInput input) =>
        _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
}

using Quantalytix.QueryEngine.Admin.Application.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms how <c>report_defaults_resolve</c> reads a default filter's request parameters. Every request that takes the defaults
/// supplies them, and the engine refuses a request that supplies a parameter of the report's model, whose values the report's rows
/// set, so a default filter comparing one would leave no request able to run the report. A request parameter compared with a
/// calculation dimension is no dimension itself.
/// </summary>
public sealed class ReportDefaultsResolveRuleTests
{
    /// <summary>The seeded tables: report <c>balance_sheet</c> over model 40, with the filterable calculation dimension <c>region</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>A default filter comparing region with a request parameter the model does not declare passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ARequestParameterComparedWithADimension_Passes()
    {
        await FilterRegionByAsync("regionKey");

        Assert.Empty(await _fixture.EvaluateAsync(new ReportDefaultsResolveRule()));
    }

    /// <summary>A default filter comparing a parameter the model declares is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AParameterOfTheModel_IsReported()
    {
        await _fixture.ParameterService().ReplaceAsync(CalculationModelId, DayCount(), TestContext.Current.CancellationToken);
        await FilterRegionByAsync("dayCount");

        Assert.Equal(
            ["Report 'balance_sheet' filters by default on @dayCount, a parameter of its model, which its rows set and so no request can supply."],
            (await _fixture.EvaluateAsync(new ReportDefaultsResolveRule())).Select(violation => violation.Message));
    }

    /// <summary>Has the seeded report filter by default on region equal to the named request parameter.</summary>
    private Task FilterRegionByAsync(string parameter)
    {
        return _fixture.Reports.UpdateAsync(
            Report() with { DefaultProfileKey = "base", DefaultFilterJson = FilterRegionBy(parameter) }, TestContext.Current.CancellationToken);
    }

    /// <summary>A default filter comparing region with the named request parameter.</summary>
    public static string FilterRegionBy(string parameter)
    {
        return $$$"""{"operator":"eq","left":{"type":"dimension","id":"region"},"right":{"type":"parameter","id":"{{{parameter}}}"}}""";
    }
}

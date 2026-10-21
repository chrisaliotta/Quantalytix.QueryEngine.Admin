using Quantalytix.QueryEngine.Admin.Application.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>scenario_dimension</c>. A run or comparison names one scenario, a member of the report's scenario dimension, so the
/// engine must find at most one: two enabled scenario dimensions are reported, while a disabled one, which the engine never reads,
/// does not count.
/// </summary>
public sealed class ScenarioDimensionRuleTests
{
    /// <summary>The seeded tables, whose report has the region calculation dimension.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>Two enabled scenario dimensions are reported once, naming both.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TwoScenarioDimensions_AreReported()
    {
        await AddScenarioDimensionAsync(82, "budget", isEnabled: true);
        await AddScenarioDimensionAsync(83, "forecast", isEnabled: true);

        var violation = Assert.Single(await _fixture.EvaluateAsync(new ScenarioDimensionRule()));

        Assert.Equal("Report 'balance_sheet' has scenario dimensions 'budget', 'forecast'; a report has at most one.", violation.Message);
    }

    /// <summary>One enabled scenario dimension beside a disabled one passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OneEnabledScenarioDimension_Passes()
    {
        await AddScenarioDimensionAsync(82, "budget", isEnabled: true);
        await AddScenarioDimensionAsync(83, "forecast", isEnabled: false);

        Assert.Empty(await _fixture.EvaluateAsync(new ScenarioDimensionRule()));
    }

    /// <summary>Adds a scenario dimension on the region role, neither groupable nor filterable.</summary>
    private Task AddScenarioDimensionAsync(long calculationDimensionId, string dimensionKey, bool isEnabled)
    {
        return _fixture.Dimensions.InsertAsync(
            CalculationDimension(calculationDimensionId, dimensionKey, RegionRoleId) with
            {
                IsGroupable = false, IsFilterable = false, IsEnabled = isEnabled, IsScenario = true
            },
            TestContext.Current.CancellationToken);
    }
}

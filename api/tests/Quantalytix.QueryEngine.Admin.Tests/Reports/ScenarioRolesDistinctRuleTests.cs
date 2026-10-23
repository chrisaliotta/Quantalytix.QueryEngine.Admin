using Quantalytix.QueryEngine.Admin.Application.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>scenario_roles_distinct</c>. A run names one member of each scenario and reads it alone, so a calculation dimension
/// that broke out or filtered by the same role would contradict it: a scenario on the role an enabled calculation dimension reads is
/// reported, while a disabled calculation dimension, which the engine never reads, does not count.
/// </summary>
public sealed class ScenarioRolesDistinctRuleTests
{
    /// <summary>The seeded tables, whose report has the region calculation dimension.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>A scenario on the region role is reported once, naming the role and the calculation dimension that reads it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ScenarioOnACalculationDimensionsRole_IsReported()
    {
        await _fixture.Scenarios.InsertAsync(Scenario(RegionRoleId), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await _fixture.EvaluateAsync(new ScenarioRolesDistinctRule()));

        Assert.Equal(
            "Report 'balance_sheet' reads role 'region' as a scenario and through calculation dimension 'region'; " +
            "a scenario's member is chosen per run, never broken out or filtered by.",
            violation.Message);
    }

    /// <summary>The same scenario passes once the region calculation dimension is disabled.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ScenarioOnADisabledCalculationDimensionsRole_Passes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Scenarios.InsertAsync(Scenario(RegionRoleId), cancellationToken);
        await _fixture.Dimensions.UpdateAsync(CalculationDimension(81, "region", RegionRoleId) with { IsEnabled = false }, cancellationToken);

        Assert.Empty(await _fixture.EvaluateAsync(new ScenarioRolesDistinctRule()));
    }
}

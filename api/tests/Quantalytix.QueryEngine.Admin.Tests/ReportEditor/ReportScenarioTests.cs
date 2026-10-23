using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms that a report's scenario list is written through the report gate. The engine reads an enabled report's scenarios at
/// once, so a list that would break a report rule, such as a scenario on the role a calculation dimension reads, is refused and
/// nothing is written; a disabled report takes the list whole, because enabling checks the result.
/// </summary>
public sealed class ReportScenarioTests
{
    /// <summary>A scenario on the account role, which calculation dimension account reads, is refused while the report is enabled and taken while it is disabled.</summary>
    /// <returns>A task that completes when the writes are checked.</returns>
    [Fact]
    public async Task ScenarioBreakingARule_IsRefusedOnlyWhileTheReportIsEnabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        await fixture.CalculationDimensions.InsertAsync(ReportRows.CalculationDimension(81, "account", AccountRoleId), cancellationToken);
        await fixture.SetEnabledAsync(true);
        var input = new ReportScenariosInput { Scenarios = [new ReportScenarioInput { RoleId = AccountRoleId }] };

        var violation = Assert.Single(await fixture.ScenarioService().ReplaceAsync(ReportRows.ReportId, input, cancellationToken));

        Assert.Equal(ScenarioRolesDistinctRule.Code, violation.Rule);
        Assert.Empty(fixture.Scenarios.All);

        await fixture.SetEnabledAsync(false);
        Assert.Empty(await fixture.ScenarioService().ReplaceAsync(ReportRows.ReportId, input, cancellationToken));
        Assert.Equal([AccountRoleId], fixture.Scenarios.All.Select(scenario => scenario.RoleId));
    }
}

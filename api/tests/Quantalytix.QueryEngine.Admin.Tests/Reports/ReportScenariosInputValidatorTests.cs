using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms the scenario list check: every role exists, the list names each role once, and each role is the only role of its
/// dimension. Requests name each scenario by its role's key, so a role named twice would be one scenario listed as two, and a
/// scenario's dimension has exactly one role, so the scenario is its dimension.
/// </summary>
public sealed class ReportScenariosInputValidatorTests
{
    /// <summary>
    /// The validator over the plan version and rate shock roles, each its dimension's only role, and the strategy dimension's two
    /// roles, strategy and priorStrategy.
    /// </summary>
    private readonly ReportScenariosInputValidator _validator = new(new InMemoryTableGateway<DimensionRoleRecord>(
        DimensionRows.Role(87, "planVersion", 610),
        DimensionRows.Role(88, "rateShock", 620),
        DimensionRows.Role(89, "strategy", 630),
        DimensionRows.Role(90, "priorStrategy", 630)));

    /// <summary>An unknown role fails on its entry, and a repeated role fails the list once.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task UnknownAndRepeatedRoles_Fail()
    {
        var result = await ValidateAsync(87, 88, 99, 87);

        result.ShouldHaveValidationErrorFor("Scenarios[2].RoleId").WithErrorMessage("Role 99 does not exist.");
        result.ShouldHaveValidationErrorFor(input => input.Scenarios).WithErrorMessage("'Scenarios' must name each role once.");
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>A role that shares its dimension with another role is refused, naming the other role.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RoleOfADimensionWithAnotherRole_Fails()
    {
        var result = await ValidateAsync(87, 89);

        result.ShouldHaveValidationErrorFor("Scenarios[1].RoleId")
            .WithErrorMessage("Role 'strategy' shares its dimension with 'priorStrategy', and a scenario's dimension has exactly one role.");
        Assert.Single(result.Errors);
    }

    /// <summary>Validates a scenario list naming the roles in order.</summary>
    private Task<TestValidationResult<ReportScenariosInput>> ValidateAsync(params long[] roleIds)
    {
        var input = new ReportScenariosInput { Scenarios = [.. roleIds.Select(roleId => new ReportScenarioInput { RoleId = roleId })] };
        return _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }
}

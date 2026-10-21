using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that a scenario dimension is neither groupable nor filterable. Its members are plan versions, which a run never adds
/// together: a run names its one version as the scenario instead of breaking out or filtering by it.
/// </summary>
public sealed class CalculationDimensionInputValidatorTests
{
    /// <summary>The validator for a new calculation dimension of report 60, over the scenario role.</summary>
    private readonly CalculationDimensionInputValidator _validator = new(
        new ContentKeys(new FixedRequestRoute(new Dictionary<string, long> { ["reportId"] = ReportId })),
        new InMemoryTableGateway<CalculationDimensionRecord>(),
        new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(88, "scenario", 620)));

    /// <summary>A groupable, filterable scenario dimension fails on both flags; without them, or as an ordinary dimension, it passes.</summary>
    /// <param name="isScenario">Whether it is the scenario dimension.</param>
    /// <param name="isGroupableAndFilterable">Whether runs may break out and filter by it.</param>
    /// <param name="valid">Whether the input passes.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task ScenarioDimension_IsNeitherGroupableNorFilterable(bool isScenario, bool isGroupableAndFilterable, bool valid)
    {
        var input = new CalculationDimensionInput
        {
            DimensionKey = "scenario",
            DisplayName = "Scenario",
            RoleId = 88,
            GroupingMode = GroupingModes.Member,
            IsGroupable = isGroupableAndFilterable,
            IsFilterable = isGroupableAndFilterable,
            IsEnabled = true,
            IsScenario = isScenario
        };

        var result = await _validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        if (valid)
        {
            result.ShouldNotHaveAnyValidationErrors();
            return;
        }
        result.ShouldHaveValidationErrorFor(dimension => dimension.IsGroupable);
        result.ShouldHaveValidationErrorFor(dimension => dimension.IsFilterable);
    }
}

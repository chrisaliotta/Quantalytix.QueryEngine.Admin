using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>calculation_grouping_resolve</c>: a calculation dimension that groups at a level or under an ancestor names a level or
/// member of the published version of its role's template, which the engine resolves the keys in; a key that version lacks, or a
/// template with no published version, is reported, because the calculation dimension would group nothing.
/// </summary>
public sealed class CalculationGroupingResolveRuleTests
{
    /// <summary>The seeded tables, with version 601 of the region dimension, published, holding level <c>region</c> and member <c>west</c>.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>Grouping at the region level resolves.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task GroupingKeysOfThePublishedVersion_Pass()
    {
        await SeedRegionsAsync();
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(81, "region", RegionRoleId) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "region" }, TestContext.Current.CancellationToken);

        Assert.Empty(await _fixture.EvaluateAsync(new CalculationGroupingResolveRule()));
    }

    /// <summary>A level and an ancestor the published version lacks are each reported, as is a template with no published version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysTheVersionLacks_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedRegionsAsync();
        await _fixture.Roles.InsertAsync(DimensionRows.Role(87, "product", 610), cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(81, "region", RegionRoleId) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "district" }, cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(82, "east", RegionRoleId) with { GroupingMode = GroupingModes.Ancestor, GroupingAncestorMemberKey = "east" }, cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(83, "product", 87) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "line" }, cancellationToken);

        var violations = await _fixture.EvaluateAsync(new CalculationGroupingResolveRule());

        Assert.Equal(
            ["Report 'balance_sheet' calculation dimension 'east' groups under ancestor member 'east', which is not a member of v1, the published version of its role's dimension.",
             "Report 'balance_sheet' calculation dimension 'product' names levels or members of its role's dimension, which has no published version.",
             "Report 'balance_sheet' calculation dimension 'region' groups at level 'district', which is not a level of v1, the published version of its role's dimension."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>Publishes version 601 of the region dimension with the level <c>region</c> and the member <c>west</c>.</summary>
    private async Task SeedRegionsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.DimensionVersions.InsertAsync(DimensionRows.Version(601, 600, 1, PublicationStatuses.Published), cancellationToken);
        await _fixture.Levels.InsertAsync(DimensionRows.Level(1, 601, "region", 1), cancellationToken);
        await _fixture.Members.InsertAsync(DimensionRows.Member(11, 601, "west"), cancellationToken);
    }
}

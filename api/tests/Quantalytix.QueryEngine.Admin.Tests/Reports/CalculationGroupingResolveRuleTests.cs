using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
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
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>Version 601 of the region dimension, published, with the level <c>region</c> and the member <c>west</c>.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(DimensionRows.Version(601, 600, 1, PublicationStatuses.Published));

    /// <summary>The region dimension's level.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(DimensionRows.Level(1, 601, "region", 1));

    /// <summary>The region dimension's member.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(DimensionRows.Member(1, 601, "west"));

    /// <summary>Grouping at the region level resolves.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task GroupingKeysOfThePublishedVersion_Pass()
    {
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(81, "region", RegionRoleId) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "region" }, TestContext.Current.CancellationToken);

        Assert.Empty(await EvaluateAsync(Rule(), ReportVersionId));
    }

    /// <summary>A level and an ancestor the published version lacks are each reported, as is a template with no published version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysTheVersionLacks_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Roles.InsertAsync(DimensionRows.Role(87, "product", 610), cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(81, "region", RegionRoleId) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "district" }, cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(82, "east", RegionRoleId) with { GroupingMode = GroupingModes.Ancestor, GroupingAncestorMemberKey = "east" }, cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(83, "product", 87) with { GroupingMode = GroupingModes.Level, GroupingLevelKey = "line" }, cancellationToken);

        var violations = await EvaluateAsync(Rule(), ReportVersionId);

        Assert.Equal(
            ["Report 'balance_sheet' calculation dimension 'east' groups under ancestor member 'east', which is not a member of v1, the published version of its role's dimension.",
             "Report 'balance_sheet' calculation dimension 'product' names levels or members of its role's dimension, which has no published version.",
             "Report 'balance_sheet' calculation dimension 'region' groups at level 'district', which is not a level of v1, the published version of its role's dimension."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The rule over the fixture's report and roles and the region dimension's tables.</summary>
    private CalculationGroupingResolveRule Rule()
    {
        return new CalculationGroupingResolveRule(_fixture.Reader(), _fixture.Roles, new PublishedDimensionReader(_versions, _levels, _members, new PublicationSet()));
    }
}

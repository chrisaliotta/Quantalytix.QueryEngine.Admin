using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms how <c>report_defaults_resolve</c> resolves a report's default comparisons, which every request that names none makes:
/// each runs under an enabled profile of the report, names a version the published version of the report's scenario dimension
/// holds as an assignable member, or a folder with one under it for the latest version, and reads a captured historical
/// assumption set rather than the active one. A key that does not resolve would fail every such request.
/// </summary>
public sealed class ReportDefaultsResolveRuleTests
{
    /// <summary>The scenario role, whose template is dimension 620.</summary>
    private const long ScenarioRoleId = 88;

    /// <summary>
    /// The seeded tables. Version 621 of dimension 620, published, files budget versions <c>BUD_FY26_V1</c> and <c>BUD_FY26_V2</c>
    /// under folder <c>FY26</c> under folder <c>BUDGETS</c>, and folder <c>FORECASTS</c> holds no version yet.
    /// </summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>Comparisons that name an enabled profile, a budget version, the latest version under a folder and a captured set pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ComparisonsThatResolve_Pass()
    {
        await SeedScenarioDimensionAsync();
        await SetComparisonsAsync("""
            [{"key":"bud","scenario":"BUD_FY26_V2","transforms":["change"]},
             {"key":"latest","profileKey":"base","scenario":{"latest":"BUDGETS"},"assumptionSet":"close_2025","transforms":["comparison"]}]
            """);

        Assert.Empty(await _fixture.EvaluateAsync(new ReportDefaultsResolveRule()));
    }

    /// <summary>
    /// A profile the report lacks, a folder named as a version, a folder with no version under it, and the active set, which is not a
    /// captured set, are each reported.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysThatDoNotResolve_AreReported()
    {
        await SeedScenarioDimensionAsync();
        await SetComparisonsAsync("""
            [{"key":"bud","profileKey":"plan","scenario":"BUDGETS","transforms":["change"]},
             {"key":"fc","scenario":{"latest":"FORECASTS"},"assumptionSet":"active","transforms":["change"]}]
            """);

        var violations = await _fixture.EvaluateAsync(new ReportDefaultsResolveRule());

        Assert.Equal(
            ["Report 'balance_sheet' default comparison 'bud' runs under profile 'plan', which is no enabled profile of the report.",
             "Report 'balance_sheet' default comparison 'bud' names scenario 'BUDGETS', which is no assignable member of v1, the published version of scenario dimension 'scenario'.",
             "Report 'balance_sheet' default comparison 'fc' names the latest scenario under folder 'FORECASTS', which has no assignable member under it in v1, the published version of scenario dimension 'scenario'.",
             "Report 'balance_sheet' default comparison 'fc' reads assumption set 'active', which is no captured assumption set."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A comparison that names a scenario in a report without a scenario dimension is reported, because no source could read it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task ScenarioWithoutAScenarioDimension_IsReported()
    {
        await SetComparisonsAsync("""[{"key":"bud","scenario":"BUD_FY26_V2","transforms":["change"]}]""");

        var violation = Assert.Single(await _fixture.EvaluateAsync(new ReportDefaultsResolveRule()));

        Assert.Equal("Report 'balance_sheet' default comparison 'bud' names scenario 'BUD_FY26_V2', but the report has no scenario dimension.", violation.Message);
    }

    /// <summary>Stores the report's default settings with the given comparisons.</summary>
    private async Task SetComparisonsAsync(string comparisons)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var report = await _fixture.Reports.GetAsync(new { ReportId }, cancellationToken);
        await _fixture.Reports.UpdateAsync(report with { DefaultSettingsJson = $$"""{"comparisons":{{comparisons}}}""" }, cancellationToken);
    }

    /// <summary>Adds the scenario role, its template's published version, and the report's scenario dimension on it.</summary>
    private async Task SeedScenarioDimensionAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Roles.InsertAsync(DimensionRows.Role(ScenarioRoleId, "scenario", 620), cancellationToken);
        await _fixture.DimensionVersions.InsertAsync(DimensionRows.Version(621, 620, 1, PublicationStatuses.Published), cancellationToken);
        await _fixture.Members.InsertManyAsync(
        [
            DimensionRows.Member(31, 621, "BUDGETS") with { IsAssignable = false },
            DimensionRows.Member(32, 621, "FY26", parentId: 31) with { IsAssignable = false },
            DimensionRows.Member(33, 621, "BUD_FY26_V1", parentId: 32),
            DimensionRows.Member(34, 621, "BUD_FY26_V2", parentId: 32),
            DimensionRows.Member(35, 621, "FORECASTS") with { IsAssignable = false }
        ], cancellationToken);
        await _fixture.Dimensions.InsertAsync(
            CalculationDimension(82, "scenario", ScenarioRoleId) with { IsGroupable = false, IsFilterable = false, IsScenario = true }, cancellationToken);
    }
}

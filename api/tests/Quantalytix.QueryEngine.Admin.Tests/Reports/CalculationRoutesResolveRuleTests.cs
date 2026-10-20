using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>calculation_routes_resolve</c>: a route must read an enabled field of the pinned dataset version, a field route one
/// that allows the grouping the dimension does, and a bridge route one whose binding groups members of the bridge's source dimension,
/// through a bridge that sends each row to one member when the dimension is filterable. A report breaks out or filters every source
/// by its route, so a wrong field would misalign the sources.
/// </summary>
public sealed class CalculationRoutesResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>GL's region route produces the region dimension.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteProducingTheDimension_Passes()
    {
        Assert.Empty(await EvaluateAsync(Rule(), ReleaseId));
    }

    /// <summary>A route to a field GL lacks is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteToAMissingField_IsReported()
    {
        await _fixture.Routes.InsertAsync(Route(91, 81, GlDatasetId, "branch"), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(), ReleaseId));

        Assert.Equal("Report 'balance_sheet' dimension 'region' routes to 'branch' of 'gl', which is no enabled field.", violation.Message);
    }

    /// <summary>A field route to <c>memo</c>, which cannot be grouped, is reported for the groupable region dimension.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task FieldRouteToAnUngroupableField_IsReported()
    {
        await _fixture.Routes.InsertAsync(Route(91, 81, GlDatasetId, "memo", RouteKinds.Field), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(), ReleaseId));

        Assert.Equal(
            "Report 'balance_sheet' dimension 'region' routes to 'memo' of 'gl', which does not allow the grouping and filtering the dimension does.",
            violation.Message);
    }

    /// <summary>
    /// A bridge route from <c>region</c>, whose binding groups regions rather than the bridge's accounts, is reported; so is a route
    /// of the filterable region dimension through a bridge that can send a row to several members.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BridgeRoutes_NeedTheBridgesSourceBindingAndOneMemberPerRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Routes.InsertAsync(Route(91, 81, GlDatasetId, "region", ClassificationKinds.Bridge) with { BridgeId = AccountBridgeId }, cancellationToken);
        await _fixture.BridgeVersions.InsertAsync(
            BridgeRows.BridgeVersion(96, AccountBridgeId, 1, Cardinalities.ManyToMany, AllocationModes.Replicate, PublicationStatuses.Published), cancellationToken);

        var violations = await EvaluateAsync(Rule(), ReleaseId);

        Assert.Equal(
            ["Report 'balance_sheet' dimension 'region' routes to 'region' of 'gl', whose binding groups no members of the source dimension of bridge 'zz_map_bridge_95'.",
             "Report 'balance_sheet' dimension 'region' routes to 'region' of 'gl' through a bridge that can send a row to several members, so it cannot be filtered on."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The release rule over the fixture's tables.</summary>
    private CalculationRoutesResolveRule Rule()
    {
        return new CalculationRoutesResolveRule(VersionKinds.SemanticRelease, _fixture.Reader());
    }
}

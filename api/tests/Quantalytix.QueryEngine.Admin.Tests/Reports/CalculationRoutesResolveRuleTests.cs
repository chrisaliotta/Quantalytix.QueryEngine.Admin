using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>calculation_routes_resolve</c>: a semantic route must name a field bound to the calculation dimension's canonical
/// dimension, and a field route a field that allows the grouping the dimension does. A report breaks out or filters every
/// source by the route's field, so a wrong field would misalign the sources.
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

    /// <summary>A semantic route to the report-item field, which is bound to another dimension, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteToAFieldOfAnotherDimension_IsReported()
    {
        await _fixture.Routes.InsertAsync(Route(91, 81, GlDatasetId, "gl_account"), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(Rule(), ReleaseId));

        Assert.Equal(
            "Report 'balance_sheet' dimension 'region' routes to 'gl_account' of 'gl', which has no canonical binding into the dimension's canonical dimension.",
            violation.Message);
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

    /// <summary>The release rule over the fixture's tables.</summary>
    private CalculationRoutesResolveRule Rule()
    {
        return new CalculationRoutesResolveRule(VersionKinds.SemanticRelease, _fixture.Reader());
    }
}

using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseReportFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>calculation_routes_resolve</c>: a semantic route must name a field bound to the calculation dimension's canonical
/// dimension, a field route a field that allows the grouping the dimension does, and all semantic routes of a dimension must
/// select the same dimension version. A report breaks out or filters every source by the route's field, so a wrong field, or
/// two sources resolving different member versions, would misalign the sources.
/// </summary>
public sealed class CalculationRoutesResolveRuleTests
{
    /// <summary>The seeded tables.</summary>
    private readonly ReleaseReportFixture _fixture = new();

    /// <summary>GL's region route produces the region dimension.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteProducingTheDimension_Passes()
    {
        Assert.Empty(await EvaluateAsync(new CalculationRoutesResolveRule(_fixture.Reader())));
    }

    /// <summary>A semantic route to the report-item field, which is bound to another dimension, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RouteToAFieldOfAnotherDimension_IsReported()
    {
        await _fixture.Routes.InsertAsync(Route(91, 81, GlDatasetId, "gl_account"), TestContext.Current.CancellationToken);

        var violation = Assert.Single(await EvaluateAsync(new CalculationRoutesResolveRule(_fixture.Reader())));

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

        var violation = Assert.Single(await EvaluateAsync(new CalculationRoutesResolveRule(_fixture.Reader())));

        Assert.Equal(
            "Report 'balance_sheet' dimension 'region' routes to 'memo' of 'gl', which does not allow the grouping and filtering the dimension does.",
            violation.Message);
    }

    /// <summary>GL's region binding is unpinned while the loans region binding pins version 601, so the two routes disagree.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RoutesSelectingDifferentVersions_AreReported()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.PinLoansAsync(501);
        await _fixture.Bindings.InsertAsync(
            Binding(36, LoansVersionId, "Loan region", 600, BindingKinds.Direct) with { SourceFieldId = 7, PinnedDimensionVersionId = 601 },
            cancellationToken);
        await _fixture.Routes.InsertAsync(Route(92, 81, LoansDatasetId, "region"), cancellationToken);

        var violation = Assert.Single(await EvaluateAsync(new CalculationRoutesResolveRule(_fixture.Reader())));

        Assert.Equal("Report 'balance_sheet' dimension 'region' routes select different dimension versions; they must agree.", violation.Message);
    }
}

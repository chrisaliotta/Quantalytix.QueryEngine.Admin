using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that a new dimension version clones only a version of the route's dimension, at the start of the request. A
/// clone copies the source's levels, members and range bands and the mapping versions pinned to it, so a version of another
/// dimension would give this dimension a draft of another hierarchy, with mapping drafts of the other dimension's sets.
/// Datasets, mapping sets and bridges check their clone sources the same way.
/// </summary>
public sealed class CloneSourceTests
{
    /// <summary>The dimension the route names.</summary>
    private const long RouteDimensionId = 100;

    /// <summary>A version of the route's dimension.</summary>
    private const long OwnVersionId = 101;

    /// <summary>A version of another dimension.</summary>
    private const long ForeignVersionId = 201;

    /// <summary>The validator under test, for a request to <c>dimensions/100/versions</c>.</summary>
    private readonly NewDimensionVersionInputValidator _validator = new(
        new InMemoryTableGateway<DimensionVersionRecord>(
            DimensionRows.Version(OwnVersionId, RouteDimensionId, 1, PublicationStatuses.Published, null, null),
            DimensionRows.Version(ForeignVersionId, 200, 1, PublicationStatuses.Published, null, null)),
        new FixedRequestRoute(new Dictionary<string, long> { ["dimensionId"] = RouteDimensionId }));

    /// <summary>A version of another dimension is refused under <c>cloneFromVersionId</c>.</summary>
    /// <returns>A task that completes when the input is checked.</returns>
    [Fact]
    public async Task VersionOfAnotherDimension_IsRejected()
    {
        var result = await _validator.TestValidateAsync(
            new NewDimensionVersionInput { CloneFromVersionId = ForeignVersionId }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(input => input.CloneFromVersionId);
    }

    /// <summary>A version of the route's dimension is a valid clone source.</summary>
    /// <returns>A task that completes when the input is checked.</returns>
    [Fact]
    public async Task VersionOfTheRoutesDimension_Passes()
    {
        var result = await _validator.TestValidateAsync(
            new NewDimensionVersionInput { CloneFromVersionId = OwnVersionId }, cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

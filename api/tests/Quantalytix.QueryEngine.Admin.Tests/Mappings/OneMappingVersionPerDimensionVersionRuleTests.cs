using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;
using static Quantalytix.QueryEngine.Admin.Tests.Mappings.MappingRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms <c>one_mapping_version_per_dimension_version</c>: a mapping version cannot be validated while another
/// non-retired version of its set (a draft, validated or published one) pins the same dimension version, while retired
/// versions, versions pinned to other dimension versions and other sets' versions do not count. A mapped binding resolves
/// exactly one mapping version from its set and resolved dimension version, which two candidates would make ambiguous.
/// </summary>
public sealed class OneMappingVersionPerDimensionVersionRuleTests
{
    /// <summary>The mapping set whose versions are checked.</summary>
    private const long MappingSetId = 300;

    /// <summary>The dimension version most of the set's versions pin.</summary>
    private const long SharedVersionId = 10;

    /// <summary>
    /// The mapping versions: set 300's v1 (published), v2 (draft, validated next), v3 (retired) and v5 (validated) pin version
    /// 10, and its v4 (draft) pins version 11; set 301's v1 also pins version 10.
    /// </summary>
    private readonly InMemoryTableGateway<MappingVersionRecord> _versions = new(
        MappingVersion(401, MappingSetId, 1, SharedVersionId, PublicationStatuses.Published),
        MappingVersion(402, MappingSetId, 2, SharedVersionId, PublicationStatuses.Draft),
        MappingVersion(403, MappingSetId, 3, SharedVersionId, PublicationStatuses.Retired),
        MappingVersion(404, MappingSetId, 4, 11, PublicationStatuses.Draft),
        MappingVersion(405, MappingSetId, 5, SharedVersionId, PublicationStatuses.Validated),
        MappingVersion(411, 301, 1, SharedVersionId, PublicationStatuses.Published));

    /// <summary>The dimension versions the mapping versions pin.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        Version(SharedVersionId, 1, 2, PublicationStatuses.Published, Utc(2026, 1, 1), null),
        Version(11, 1, 3, PublicationStatuses.Published, Utc(2026, 7, 1), null));

    /// <summary>Validating v2 reports the published v1 and the validated v5 of the same set on the same dimension version, in version order.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task NonRetiredSiblingsOnTheSameDimensionVersion_AreReported()
    {
        var rule = new OneMappingVersionPerDimensionVersionRule(_versions, _dimensionVersions);

        var violations = await rule.EvaluateAsync(402, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal(OneMappingVersionPerDimensionVersionRule.Code, violation.Rule));
        Assert.Equal(
            [
                "Mapping version v1 (published) of this set already pins dimension version v2.",
                "Mapping version v5 (validated) of this set already pins dimension version v2."
            ],
            violations.Select(violation => violation.Message));
    }

    /// <summary>The only version of the set on its dimension version passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OnlyVersionOnItsDimensionVersion_Passes()
    {
        var rule = new OneMappingVersionPerDimensionVersionRule(_versions, _dimensionVersions);

        var violations = await rule.EvaluateAsync(404, TestContext.Current.CancellationToken);

        Assert.Empty(violations);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>bound_dimensions_resolvable</c>: a binding whose dimension has no published version is reported, and a time
/// binding passes without any dimension version. The active assumption set pins each dimension's published version and the
/// engine resolves bindings in it, but it derives calendar periods for time bindings from the bound field, so requiring
/// versions there would block releases the engine can serve.
/// </summary>
public sealed class BoundDimensionsResolvableRuleTests
{
    /// <summary>The pinned dataset version.</summary>
    private const long DatasetVersionId = 11;

    /// <summary>The release pins loans v1.</summary>
    private readonly InMemoryTableGateway<ReleaseDatasetRecord> _manifest = new(Pin(1, DatasetVersionId));

    /// <summary>The pinned dataset.</summary>
    private readonly InMemoryTableGateway<DatasetRecord> _datasets = new(Dataset(1, "loans"));

    /// <summary>Branch, whose v1 is published and v2 a draft; Product, which has only a draft; Calendar, which has no version.</summary>
    private readonly InMemoryTableGateway<DimensionRecord> _dimensions = new(Dimension(100, "Branch"), Dimension(200, "Product"), Dimension(300, "Calendar"));

    /// <summary>The versions of Branch and Product.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionVersion(101, 100, 1, PublicationStatuses.Published),
        DimensionVersion(102, 100, 2, PublicationStatuses.Draft),
        DimensionVersion(201, 200, 1, PublicationStatuses.Draft));

    /// <summary>A binding whose dimension has only a draft version is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DimensionWithoutPublishedVersion_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(Binding(32, DatasetVersionId, ProductRoleId, 200, BindingKinds.Direct)));

        Assert.Equal("bound_dimensions_resolvable", violation.Rule);
        Assert.Equal("Binding 'product' of dataset 'loans' binds dimension 'Product', which has no published version.", violation.Message);
    }

    /// <summary>A binding of the branch role resolves in Branch's published v1, and a time binding needs no dimension version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishedVersionAndTimeBinding_Pass()
    {
        Assert.Empty(await EvaluateAsync(
            Binding(31, DatasetVersionId, BranchRoleId, 100, BindingKinds.Direct),
            Binding(33, DatasetVersionId, null, 300, BindingKinds.Time) with { SourceFieldId = AsOfFieldId }));
    }

    /// <summary>Evaluates the rule with the given bindings of the pinned dataset version.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var reader = Reader(_manifest, _datasets, bindings);
        var rule = new BoundDimensionsResolvableRule(reader, _dimensionVersions, _dimensions);
        return await rule.EvaluateAsync(SemanticReleaseId, TestContext.Current.CancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>bound_dimensions_resolvable</c>: an unpinned binding whose dimension has no published version is reported,
/// and so is a binding that pins an unpublished version even when another version is published; an unpinned time binding
/// passes without any dimension version. The engine reads published dimension versions only, but derives calendar
/// periods for time bindings from the bound field, so requiring versions there would block releases the engine can serve.
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

    /// <summary>An unpinned binding whose dimension has only a draft version is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DimensionWithoutPublishedVersion_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(Binding(32, DatasetVersionId, "Product", 200, BindingKinds.Direct)));

        Assert.Equal("bound_dimensions_resolvable", violation.Rule);
        Assert.Equal("Binding 'Product' of dataset 'loans' binds dimension 'Product', which has no published version.", violation.Message);
    }

    /// <summary>A binding that pins the draft v2 of Branch is reported although Branch v1 is published.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PinToUnpublishedVersion_IsReported()
    {
        var pinned = Binding(31, DatasetVersionId, "Branch", 100, BindingKinds.Direct) with { PinnedDimensionVersionId = 102 };

        var violation = Assert.Single(await EvaluateAsync(pinned));

        Assert.Equal("Binding 'Branch' of dataset 'loans' pins dimension version 102, which is not a published version of dimension 'Branch'.", violation.Message);
    }

    /// <summary>An unpinned binding of Branch resolves to its published v1, and an unpinned time binding needs no dimension version.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishedVersionAndTimeBinding_Pass()
    {
        Assert.Empty(await EvaluateAsync(
            Binding(31, DatasetVersionId, "Branch", 100, BindingKinds.Direct),
            Binding(33, DatasetVersionId, "As of date", 300, BindingKinds.Time)));
    }

    /// <summary>Evaluates the rule with the given bindings of the pinned dataset version.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var reader = new ReleaseManifestReader(_manifest, _datasets, new InMemoryTableGateway<DimensionBindingRecord>(bindings));
        var rule = new BoundDimensionsResolvableRule(reader, _dimensionVersions, _dimensions);
        return await rule.EvaluateAsync(ReleaseId, TestContext.Current.CancellationToken);
    }
}

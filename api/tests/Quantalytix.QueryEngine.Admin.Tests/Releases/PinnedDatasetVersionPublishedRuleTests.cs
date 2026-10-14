using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>pinned_version_published</c> for releases: a manifest row that pins a draft dataset version is reported
/// with the dataset's key and the version's status, and published pins pass. The engine reads a release's datasets
/// through published versions only, so an unpublished pin would silently drop its dataset from queries.
/// </summary>
public sealed class PinnedDatasetVersionPublishedRuleTests
{
    /// <summary>The datasets the manifests pin.</summary>
    private readonly InMemoryTableGateway<DatasetRecord> _datasets = new(Dataset(1, "loans"), Dataset(2, "deposits"));

    /// <summary>Loans has a published v1 and a draft v2; deposits has a published v1.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _datasetVersions = new(
        DatasetVersion(11, 1, 1, PublicationStatuses.Published),
        DatasetVersion(12, 1, 2, PublicationStatuses.Draft),
        DatasetVersion(21, 2, 1, PublicationStatuses.Published));

    /// <summary>A release that pins the draft loans version is reported, naming the dataset and the version's status.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DraftPin_IsReported()
    {
        var violations = await EvaluateAsync(Pin(1, 12), Pin(2, 21));

        var violation = Assert.Single(violations);
        Assert.Equal("pinned_version_published", violation.Rule);
        Assert.Equal("Dataset 'loans' pins v2, which is draft; a release pins only published versions.", violation.Message);
    }

    /// <summary>A release that pins only published versions passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PublishedPins_Pass()
    {
        Assert.Empty(await EvaluateAsync(Pin(1, 11), Pin(2, 21)));
    }

    /// <summary>Evaluates the rule for a release with the given manifest rows.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params ReleaseDatasetRecord[] manifest)
    {
        var reader = new ReleaseManifestReader(new InMemoryTableGateway<ReleaseDatasetRecord>(manifest), _datasets, new InMemoryTableGateway<DimensionBindingRecord>());
        var rule = new PinnedDatasetVersionPublishedRule(reader, _datasetVersions);
        return await rule.EvaluateAsync(ReleaseId, TestContext.Current.CancellationToken);
    }
}

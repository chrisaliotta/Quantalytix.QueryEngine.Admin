using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms <c>pinned_version_belongs_to_dataset</c>: a manifest row whose dataset version belongs to another dataset, or
/// no longer exists, is reported with the row's dataset key, and rows that pin their own dataset's versions pass. The
/// manifest input accepts any IDs, and the engine joins a release's datasets on both IDs, so a mismatched row would drop
/// its dataset from queries.
/// </summary>
public sealed class PinnedVersionBelongsToDatasetRuleTests
{
    /// <summary>The datasets the manifests pin.</summary>
    private readonly InMemoryTableGateway<DatasetRecord> _datasets = new(Dataset(1, "loans"), Dataset(2, "deposits"));

    /// <summary>One published version of each dataset.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _datasetVersions = new(
        DatasetVersion(11, 1, 1, PublicationStatuses.Published),
        DatasetVersion(21, 2, 1, PublicationStatuses.Published));

    /// <summary>A row that pins a version of another dataset is reported, naming the row's dataset.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionOfAnotherDataset_IsReported()
    {
        var violations = await EvaluateAsync(Pin(1, 21), Pin(2, 21));

        var violation = Assert.Single(violations);
        Assert.Equal("pinned_version_belongs_to_dataset", violation.Rule);
        Assert.Equal("Dataset 'loans' pins dataset version 21, which is not one of its versions.", violation.Message);
    }

    /// <summary>A row that pins a dataset version that does not exist, such as a deleted draft, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MissingVersion_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(Pin(1, 11), Pin(2, 22)));

        Assert.Equal("Dataset 'deposits' pins dataset version 22, which is not one of its versions.", violation.Message);
    }

    /// <summary>Rows that pin their own dataset's versions pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OwnVersions_Pass()
    {
        Assert.Empty(await EvaluateAsync(Pin(1, 11), Pin(2, 21)));
    }

    /// <summary>Evaluates the rule for a release with the given manifest rows.</summary>
    private async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params ReleaseDatasetRecord[] manifest)
    {
        var reader = Reader(new InMemoryTableGateway<ReleaseDatasetRecord>(manifest), _datasets);
        var rule = new PinnedVersionBelongsToDatasetRule(reader, _datasetVersions);
        return await rule.EvaluateAsync(ReleaseId, TestContext.Current.CancellationToken);
    }
}

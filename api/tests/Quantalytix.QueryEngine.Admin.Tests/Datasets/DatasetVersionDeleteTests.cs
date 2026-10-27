using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that a draft dataset version a release manifest pins is not deleted, and that the 409 detail names the
/// release. A draft release may pin any version until it is validated, so without this check its manifest would keep a row
/// naming a version that no longer exists.
/// </summary>
public sealed class DatasetVersionDeleteTests
{
    /// <summary>The draft version a draft release pins.</summary>
    private const long PinnedVersionId = 100;

    /// <summary>The draft release that pins it.</summary>
    private const long SemanticReleaseId = 500;

    /// <summary>The dataset version table.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _versions = new(DatasetRows.Version(PinnedVersionId));

    /// <summary>The release manifest rows.</summary>
    private readonly InMemoryTableGateway<ReleaseDatasetRecord> _manifest = new(
        new ReleaseDatasetRecord { SemanticReleaseId = SemanticReleaseId, DatasetId = 10, DatasetVersionId = PinnedVersionId });

    /// <summary>The release table.</summary>
    private readonly InMemoryTableGateway<SemanticReleaseRecord> _releases = new(new SemanticReleaseRecord
    {
        SemanticReleaseId = SemanticReleaseId,
        ReleaseKey = "zz_rel_next",
        Status = PublicationStatuses.Draft,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A pinned draft is kept, and the detail names the release that pins it.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task PinnedDraft_IsKeptAndTheReleaseNamed()
    {
        var conflict = await Service().DeleteDraftAsync(PinnedVersionId, TestContext.Current.CancellationToken);

        Assert.Equal("Dataset version v1 cannot be deleted: pinned by releases 'zz_rel_next'.", conflict);
        Assert.Single(_versions.All);
    }

    /// <summary>The service over this test's tables, with empty version content.</summary>
    private DatasetVersionService Service()
    {
        var content = new DatasetVersionContentStore(
            new InMemoryTableGateway<FieldRecord>(),
            new InMemoryTableGateway<DatasetGrainRecord>(),
            new InMemoryTableGateway<DatasetGrainFieldRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new InMemoryTableGateway<TimeBindingRecord>(),
            new InMemoryTableGateway<MeasureRecord>(),
            new InMemoryTableGateway<MeasureComponentRecord>(),
            new InMemoryTableGateway<MeasureDependencyRecord>(),
            new InMemoryTableGateway<MeasureTimePolicyRecord>());
        var ids = new SequentialIdGenerator(1000);
        return new DatasetVersionService(_versions, _manifest, _releases, content, new DatasetVersionCloner(_versions, content, ids, TimeProvider.System), ids, TimeProvider.System);
    }
}

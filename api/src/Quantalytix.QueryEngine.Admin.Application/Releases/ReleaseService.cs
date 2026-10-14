using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Creates, lists and deletes semantic releases and edits their manifests (<c>qe_semantic_release</c>, <c>qe_release_dataset</c>).</summary>
public sealed class ReleaseService(
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Lists every release, newest first, with whether it is active and how many datasets it pins.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The release summaries.</returns>
    public async Task<IReadOnlyList<ReleaseSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await releases.ListAsync(null, cancellationToken);
        var datasetCounts = (await manifest.ListAsync(null, cancellationToken)).CountBy(row => row.ReleaseId).ToDictionary();
        var info = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        return
        [
            .. rows
                .OrderByDescending(row => row.CreatedAt)
                .ThenBy(row => row.ReleaseKey, StringComparer.Ordinal)
                .Select(row => new ReleaseSummary
                {
                    Release = row,
                    IsActive = row.ReleaseId == info.ActiveReleaseId,
                    DatasetCount = datasetCounts.GetValueOrDefault(row.ReleaseId)
                })
        ];
    }

    /// <summary>Reads one release.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The release row.</returns>
    public Task<SemanticReleaseRecord> GetAsync(long releaseId, CancellationToken cancellationToken)
    {
        return releases.GetAsync(new { ReleaseId = releaseId }, cancellationToken);
    }

    /// <summary>Creates a draft release and, when asked, copies another release's manifest rows into it.</summary>
    /// <param name="input">The validated key and optional source release.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new release row.</returns>
    public async Task<SemanticReleaseRecord> CreateAsync(ReleaseCreateInput input, CancellationToken cancellationToken)
    {
        var release = new SemanticReleaseRecord
        {
            ReleaseId = ids.NextId(),
            ReleaseKey = input.ReleaseKey,
            Status = PublicationStatuses.Draft,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await releases.InsertAsync(release, cancellationToken);
        if (input.CopyFromReleaseId is { } sourceReleaseId)
        {
            var sourceRows = await manifest.ListAsync(new { ReleaseId = sourceReleaseId }, cancellationToken);
            await manifest.InsertManyAsync([.. sourceRows.Select(row => row with { ReleaseId = release.ReleaseId })], cancellationToken);
        }

        return release;
    }

    /// <summary>Deletes a draft release: its manifest rows first, then the release.</summary>
    /// <param name="releaseId">The draft release.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the release is gone.</returns>
    public async Task DeleteAsync(long releaseId, CancellationToken cancellationToken)
    {
        await manifest.DeleteAsync(new { ReleaseId = releaseId }, cancellationToken);
        await releases.DeleteAsync(new { ReleaseId = releaseId }, cancellationToken);
    }

    /// <summary>Lists a release's manifest rows, ordered by dataset.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The manifest rows.</returns>
    public async Task<IReadOnlyList<ReleaseDatasetRecord>> ListDatasetsAsync(long releaseId, CancellationToken cancellationToken)
    {
        var rows = await manifest.ListAsync(new { ReleaseId = releaseId }, cancellationToken);
        return [.. rows.OrderBy(row => row.DatasetId)];
    }

    /// <summary>Replaces a draft release's manifest with the given rows.</summary>
    /// <param name="releaseId">The draft release.</param>
    /// <param name="input">The validated manifest.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored manifest rows, ordered by dataset.</returns>
    public async Task<IReadOnlyList<ReleaseDatasetRecord>> ReplaceDatasetsAsync(long releaseId, ReleaseManifestInput input, CancellationToken cancellationToken)
    {
        ReleaseDatasetRecord[] rows =
        [
            .. input.Datasets
                .OrderBy(row => row.DatasetId)
                .Select(row => new ReleaseDatasetRecord { ReleaseId = releaseId, DatasetId = row.DatasetId, DatasetVersionId = row.DatasetVersionId })
        ];
        await manifest.DeleteAsync(new { ReleaseId = releaseId }, cancellationToken);
        await manifest.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Lists, creates, edits and deletes datasets, the stable identities that query requests name.</summary>
public sealed class DatasetService(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<ReleaseDatasetRecord> releaseDatasets,
    ITableGateway<SemanticReleaseRecord> releases,
    DatasetVersionService versionService,
    SemanticRevision semanticRevision,
    IIdGenerator ids)
{
    /// <summary>Lists every dataset.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The datasets, sorted by key.</returns>
    public async Task<IReadOnlyList<DatasetRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await datasets.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.DatasetKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads one dataset.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dataset.</returns>
    public Task<DatasetRecord> GetAsync(long datasetId, CancellationToken cancellationToken)
    {
        return datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
    }

    /// <summary>Creates a dataset and its empty draft version 1 over the given source object.</summary>
    /// <param name="input">The new dataset.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new dataset.</returns>
    public async Task<DatasetRecord> CreateAsync(DatasetInput input, CancellationToken cancellationToken)
    {
        var dataset = new DatasetRecord
        {
            DatasetId = ids.NextId(),
            DatasetKey = input.DatasetKey,
            DisplayName = input.DisplayName,
            IsEnabled = input.IsEnabled
        };
        await datasets.InsertAsync(dataset, cancellationToken);
        var firstVersion = new DatasetVersionInput { SourceObjectKind = input.SourceObjectKind, SourceObject = input.SourceObject };
        await versionService.CreateAsync(dataset.DatasetId, firstVersion, cancellationToken);
        return dataset;
    }

    /// <summary>
    /// Changes a dataset's display name and availability, then advances the semantic revision, because the query engine
    /// caches identity rows by it.
    /// </summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="input">The new identity attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The updated dataset.</returns>
    public async Task<DatasetRecord> UpdateAsync(long datasetId, DatasetUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(datasetId, cancellationToken);
        var updated = current with { DisplayName = input.DisplayName, IsEnabled = input.IsEnabled };
        await datasets.UpdateAsync(updated, cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a dataset whose versions are all drafts and that no release lists: first its versions with their content,
    /// then the dataset.
    /// </summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the dataset was deleted; otherwise why it was kept, naming the versions or releases in the way.</returns>
    public async Task<string?> DeleteAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await GetAsync(datasetId, cancellationToken);
        var datasetVersions = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var kept = datasetVersions.Where(version => version.Status != PublicationStatuses.Draft).OrderBy(version => version.VersionNumber).ToList();
        if (kept.Count > 0)
        {
            var listed = string.Join(", ", kept.Select(version => $"v{version.VersionNumber} is {version.Status}"));
            return $"Dataset '{dataset.DatasetKey}' can only be deleted while all its versions are drafts: {listed}.";
        }

        var memberships = await releaseDatasets.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        if (memberships.Count > 0)
        {
            var listing = await releases.ListAsync(new { ReleaseId = memberships.Select(row => row.ReleaseId).ToArray() }, cancellationToken);
            var releaseKeys = string.Join(", ", listing.Select(release => $"'{release.ReleaseKey}'").Order(StringComparer.OrdinalIgnoreCase));
            return $"Dataset '{dataset.DatasetKey}' is listed by releases {releaseKeys}; remove it from them first.";
        }

        await versionService.DeleteAsync([.. datasetVersions.Select(version => version.DatasetVersionId)], cancellationToken);
        await datasets.DeleteAsync(new { DatasetId = datasetId }, cancellationToken);
        return null;
    }
}

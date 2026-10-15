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
    /// <returns>Null when the dataset was deleted; otherwise the detail naming the versions and releases in the way.</returns>
    public async Task<string?> DeleteAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await GetAsync(datasetId, cancellationToken);
        var datasetVersions = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var memberships = await releaseDatasets.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var listing = await releases.ListAsync(new { ReleaseId = memberships.Select(row => row.ReleaseId).ToArray() }, cancellationToken);
        var blockers = new DeleteBlockers($"Dataset '{dataset.DatasetKey}'");
        blockers.AddNamed(
            "it has non-draft versions",
            datasetVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        blockers.AddNamed("listed by releases", listing.Select(release => $"'{release.ReleaseKey}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await versionService.DeleteAsync([.. datasetVersions.Select(version => version.DatasetVersionId)], cancellationToken);
        await datasets.DeleteAsync(new { DatasetId = datasetId }, cancellationToken);
        return null;
    }
}

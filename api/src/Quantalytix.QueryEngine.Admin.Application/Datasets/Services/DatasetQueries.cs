using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The dataset manager's reads: the list, one dataset, a version's content, its rule check and the delete checks.</summary>
public sealed class DatasetQueries(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    DatasetReleaseReader releases,
    SourceObjectDirectory sourceObjects,
    DatasetSnapshotLoader loader,
    DatasetVersionReader reader,
    DatasetRules rules,
    TimeProvider time)
{
    /// <summary>Lists every dataset.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items, by key.</returns>
    public async Task<IReadOnlyList<DatasetListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var all = (await versions.ListAsync(null, cancellationToken)).ToLookup(version => version.DatasetId);
        var index = await releases.ReadAsync(null, cancellationToken);
        var statistics = await sourceObjects.StatisticsAsync(cancellationToken);
        return
        [
            .. (await datasets.ListAsync(null, cancellationToken))
                .OrderBy(dataset => dataset.DatasetKey, StringComparer.OrdinalIgnoreCase)
                .Select(dataset => DatasetReadModels.ListItem(dataset, all[dataset.DatasetId], index, statistics))
        ];
    }

    /// <summary>Reads one dataset.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list item.</returns>
    public async Task<DatasetListItem> GetAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return DatasetReadModels.ListItem(dataset, own, await releases.ReadAsync(datasetId, cancellationToken), await sourceObjects.StatisticsAsync(cancellationToken));
    }

    /// <summary>Reads a version's whole content as stored.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content.</returns>
    public async Task<DatasetVersion> VersionAsync(long versionId, CancellationToken cancellationToken)
    {
        return await reader.ReadAsync(await loader.LoadAsync(versionId, cancellationToken), TemporaryIds.PresentStored, cancellationToken);
    }

    /// <summary>Runs every dataset rule on a stored version and changes nothing.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rule report.</returns>
    public async Task<ValidationReport> CheckAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = await rules.EvaluateAsync(await loader.LoadAsync(versionId, cancellationToken), cancellationToken);
        return new ValidationReport { VersionId = versionId, CheckedAt = time.GetUtcNow().UtcDateTime, Violations = violations };
    }

    /// <summary>Tells whether a dataset can be deleted.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The delete check.</returns>
    public async Task<DeleteCheck> DatasetDeleteCheckAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return DatasetDeleteRules.Dataset(dataset, own, await releases.ReadAsync(datasetId, cancellationToken));
    }

    /// <summary>Tells whether a version can be deleted.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The delete check.</returns>
    public async Task<DeleteCheck> VersionDeleteCheckAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return DatasetDeleteRules.Version(version, await releases.ReadAsync(version.DatasetId, cancellationToken));
    }
}

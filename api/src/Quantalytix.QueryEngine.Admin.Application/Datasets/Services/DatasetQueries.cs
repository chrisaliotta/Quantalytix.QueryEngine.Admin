using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset manager's reads: the list of datasets with a live version, one dataset, a version's content and the delete check.
/// </summary>
public sealed class DatasetQueries(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    LiveSnapshot live,
    SnapshotHolders holders,
    SourceObjectDirectory sourceObjects,
    DatasetSnapshotLoader loader,
    DatasetVersionReader reader)
{
    /// <summary>Lists every dataset the live snapshot holds.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list items, by key.</returns>
    public async Task<IReadOnlyList<DatasetListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var liveSnapshotId = await live.IdAsync(cancellationToken);
        var all = (await versions.ListAsync(null, cancellationToken)).ToLookup(version => version.DatasetId);
        var statistics = await sourceObjects.StatisticsAsync(cancellationToken);
        return
        [
            .. (await datasets.ListAsync(null, cancellationToken))
                .Where(dataset => all[dataset.DatasetId].Any(version => version.SnapshotId == liveSnapshotId))
                .OrderBy(dataset => dataset.DatasetKey, StringComparer.OrdinalIgnoreCase)
                .Select(dataset => DatasetReadModels.ListItem(dataset, all[dataset.DatasetId], liveSnapshotId, statistics))
        ];
    }

    /// <summary>Reads one dataset.</summary>
    /// <param name="datasetId">The dataset, which the live snapshot holds.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The list item.</returns>
    public async Task<DatasetListItem> GetAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return DatasetReadModels.ListItem(dataset, own, await live.IdAsync(cancellationToken), await sourceObjects.StatisticsAsync(cancellationToken));
    }

    /// <summary>Reads a version's whole content as stored, live or frozen.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content.</returns>
    public async Task<DatasetVersion> VersionAsync(long versionId, CancellationToken cancellationToken)
    {
        return await reader.ReadAsync(await loader.LoadAsync(versionId, cancellationToken), TemporaryIds.PresentStored, cancellationToken);
    }

    /// <summary>Tells whether a dataset can be deleted.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The delete check.</returns>
    public async Task<DeleteCheck> DatasetDeleteCheckAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return DatasetDeleteRules.Dataset(dataset, await holders.FrozenKeysAsync(own.Select(version => version.SnapshotId), cancellationToken));
    }
}

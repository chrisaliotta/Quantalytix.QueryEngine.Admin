using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Reads what a release pins for its publication rules: the manifest rows with their dataset keys, and the dimension
/// bindings of the pinned dataset versions. The manifest validator guarantees that every row's dataset exists and that
/// each dataset version is pinned once.
/// </summary>
public sealed class ReleaseManifestReader(
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionBindingRecord> bindings,
    BindingNames names)
{
    /// <summary>Lists a release's manifest rows with the keys of their datasets, ordered by dataset key.</summary>
    /// <param name="semanticReleaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned datasets.</returns>
    public async Task<IReadOnlyList<PinnedDataset>> ListDatasetsAsync(long semanticReleaseId, CancellationToken cancellationToken)
    {
        var rows = await manifest.ListAsync(new { SemanticReleaseId = semanticReleaseId }, cancellationToken);
        var datasetRows = await datasets.ListAsync(new { DatasetId = rows.Select(row => row.DatasetId).ToArray() }, cancellationToken);
        var keys = datasetRows.ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        return
        [
            .. rows
                .Select(row => new PinnedDataset { DatasetKey = keys[row.DatasetId], Row = row })
                .OrderBy(pinned => pinned.DatasetKey, StringComparer.Ordinal)
        ];
    }

    /// <summary>Lists the dimension bindings of the dataset versions a release pins, ordered by dataset key and binding name.</summary>
    /// <param name="semanticReleaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned bindings.</returns>
    public async Task<IReadOnlyList<PinnedBinding>> ListBindingsAsync(long semanticReleaseId, CancellationToken cancellationToken)
    {
        var pinned = await ListDatasetsAsync(semanticReleaseId, cancellationToken);
        var datasetKeys = pinned.ToDictionary(dataset => dataset.Row.DatasetVersionId, dataset => dataset.DatasetKey);
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetKeys.Keys.ToArray() }, cancellationToken);
        var named = await names.OfAsync(rows, cancellationToken);
        return
        [
            .. rows
                .Select(binding => new PinnedBinding { DatasetKey = datasetKeys[binding.DatasetVersionId], Binding = binding, Name = named[binding.DimensionBindingId] })
                .OrderBy(binding => binding.DatasetKey, StringComparer.Ordinal)
                .ThenBy(binding => binding.Name, StringComparer.Ordinal)
        ];
    }
}

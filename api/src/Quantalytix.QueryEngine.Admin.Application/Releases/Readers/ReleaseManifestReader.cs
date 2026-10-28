using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Reads the dimension bindings of the dataset versions a manifest, stored or pending, pins, for the binding rules.</summary>
public sealed class ReleaseManifestReader(ITableGateway<DimensionBindingRecord> bindings, BindingNames names)
{
    /// <summary>Lists the bindings of a snapshot's pinned dataset versions, ordered by dataset key and binding name.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned bindings.</returns>
    public async Task<IReadOnlyList<PinnedBinding>> ListBindingsAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var keys = snapshot.Surroundings.Datasets.ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var datasetOf = snapshot.Manifest.ToDictionary(row => row.DatasetVersionId, row => row.DatasetId);
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetOf.Keys.ToArray() }, cancellationToken);
        var named = await names.OfAsync(rows, cancellationToken);
        return
        [
            .. rows
                .Select(binding => new PinnedBinding
                {
                    DatasetId = datasetOf[binding.DatasetVersionId],
                    DatasetKey = keys[datasetOf[binding.DatasetVersionId]],
                    Binding = binding,
                    Name = named[binding.DimensionBindingId]
                })
                .OrderBy(binding => binding.DatasetKey, StringComparer.Ordinal)
                .ThenBy(binding => binding.Name, StringComparer.Ordinal)
        ];
    }
}

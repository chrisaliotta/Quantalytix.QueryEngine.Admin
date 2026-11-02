using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the dataset versions a snapshot holds, indexed for resolving the keys and roles report sources name in them. A report version
/// runs against its own snapshot's dataset versions.
/// </summary>
public sealed class SnapshotDatasets(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures)
{
    /// <summary>Reads a snapshot's versions of some datasets.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="datasetIds">The datasets.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The versions by dataset ID; a dataset the snapshot does not hold is missing.</returns>
    public async Task<IReadOnlyDictionary<long, HeldDatasetVersion>> ReadAsync(long snapshotId, IReadOnlyCollection<long> datasetIds, CancellationToken cancellationToken)
    {
        var held = (await versions.ListAsync(new { SnapshotId = snapshotId, DatasetId = datasetIds.ToArray() }, cancellationToken))
            .ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        long[] versionIds = [.. held.Values];
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = held.Keys.ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = (await bindings.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToLookup(binding => binding.DatasetVersionId);
        var timeOf = (await timeBindings.ListAsync(new { DimensionBindingId = versionBindings.SelectMany(group => group).Select(binding => binding.DimensionBindingId).ToArray() }, cancellationToken));
        var versionFields = (await fields.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToLookup(field => field.DatasetVersionId);
        var versionMeasures = (await measures.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToLookup(measure => measure.DatasetVersionId);
        return held.ToDictionary(
            pair => pair.Key,
            pair => HeldDatasetVersion.Of(datasetKeys[pair.Key], versionFields[pair.Value], [.. versionBindings[pair.Value]], timeOf, versionMeasures[pair.Value]));
    }
}

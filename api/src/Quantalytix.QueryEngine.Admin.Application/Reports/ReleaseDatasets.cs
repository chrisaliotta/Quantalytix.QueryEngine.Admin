using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the dataset versions a release pins, indexed for resolving the keys and roles report sources name in them. Reports run
/// against whichever release is active, so sources are written against the active release's versions.
/// </summary>
public sealed class ReleaseDatasets(
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures)
{
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>The active release's dataset versions this request has read, which no request changes while it validates a source.</summary>
    private readonly Dictionary<long, PinnedDatasetVersion?> _active = [];

    /// <summary>Reads the versions a release pins of some datasets.</summary>
    /// <param name="semanticReleaseId">The release; null for the active release.</param>
    /// <param name="datasetIds">The datasets.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned versions by dataset ID; a dataset the release does not pin is missing.</returns>
    public async Task<IReadOnlyDictionary<long, PinnedDatasetVersion>> ReadAsync(long? semanticReleaseId, IReadOnlyCollection<long> datasetIds, CancellationToken cancellationToken)
    {
        var release = semanticReleaseId ?? (await databaseInfo.GetAsync(new { SingletonId }, cancellationToken)).ActiveSemanticReleaseId;
        var datasetVersions = (await manifest.ListAsync(new { SemanticReleaseId = release, DatasetId = datasetIds.ToArray() }, cancellationToken))
            .ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        long[] versionIds = [.. datasetVersions.Values];
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = datasetVersions.Keys.ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = await bindings.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken);
        var timeIds = (await timeBindings.ListAsync(new { DimensionBindingId = versionBindings.Select(binding => binding.DimensionBindingId).ToArray() }, cancellationToken))
            .Select(extension => extension.DimensionBindingId).ToHashSet();
        var versionFields = (await fields.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToLookup(field => field.DatasetVersionId);
        var versionMeasures = (await measures.ListAsync(new { DatasetVersionId = versionIds }, cancellationToken)).ToLookup(measure => measure.DatasetVersionId);
        var bindingsByVersion = versionBindings.ToLookup(binding => binding.DatasetVersionId);
        return datasetVersions.ToDictionary(pair => pair.Key, pair => new PinnedDatasetVersion
        {
            DatasetKey = datasetKeys[pair.Key],
            Fields = versionFields[pair.Value].ToDictionary(field => field.FieldKey, StringComparer.Ordinal),
            Bindings = [.. bindingsByVersion[pair.Value]],
            TimeBindingIds = bindingsByVersion[pair.Value].Where(binding => binding.BindingKind == BindingKinds.Time && timeIds.Contains(binding.DimensionBindingId))
                .Select(binding => binding.DimensionBindingId).ToHashSet(),
            Measures = versionMeasures[pair.Value].ToDictionary(measure => measure.MeasureKey, StringComparer.Ordinal)
        });
    }

    /// <summary>Reads the active release's version of one dataset.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned version, or null when the active release does not pin the dataset.</returns>
    public async Task<PinnedDatasetVersion?> ActiveAsync(long datasetId, CancellationToken cancellationToken)
    {
        if (!_active.TryGetValue(datasetId, out var dataset))
            _active[datasetId] = dataset = (await ReadAsync(null, [datasetId], cancellationToken)).GetValueOrDefault(datasetId);
        return dataset;
    }
}

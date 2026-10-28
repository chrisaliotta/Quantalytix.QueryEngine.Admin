using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Reads the datasets a manifest can pin, each version with its bindings and measures, as the manifest editor shows them.</summary>
public sealed class ManifestDatasetReader(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<MeasureRecord> measures,
    BindingNames names)
{
    /// <summary>Every dataset by key, its versions newest first.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The datasets.</returns>
    public async Task<IReadOnlyList<ManifestDataset>> ListAsync(CancellationToken cancellationToken)
    {
        var versionsOf = (await versions.ListAsync(null, cancellationToken)).ToLookup(version => version.DatasetId);
        var allBindings = await bindings.ListAsync(null, cancellationToken);
        var named = await names.OfAsync(allBindings, cancellationToken);
        var bindingsOf = allBindings.ToLookup(binding => binding.DatasetVersionId);
        var measuresOf = (await measures.ListAsync(null, cancellationToken)).ToLookup(measure => measure.DatasetVersionId);
        return
        [
            .. (await datasets.ListAsync(null, cancellationToken)).OrderBy(dataset => dataset.DatasetKey, StringComparer.Ordinal).Select(dataset => new ManifestDataset
            {
                Id = dataset.DatasetId,
                Key = dataset.DatasetKey,
                DisplayName = dataset.DisplayName,
                IsEnabled = dataset.IsEnabled,
                Versions =
                [
                    .. versionsOf[dataset.DatasetId].OrderByDescending(version => version.VersionNumber).Select(version => new ManifestDatasetVersion
                    {
                        Id = version.DatasetVersionId,
                        VersionNumber = version.VersionNumber,
                        Status = version.Status,
                        PublishedAt = version.PublishedAt,
                        Bindings =
                        [
                            .. bindingsOf[version.DatasetVersionId]
                                .Select(binding => new ManifestBinding
                                {
                                    DisplayName = named[binding.DimensionBindingId],
                                    Kind = binding.BindingKind,
                                    DimensionId = binding.DimensionId,
                                    MappingSetId = binding.MappingSetId,
                                    BridgeId = binding.BridgeId
                                })
                                .OrderBy(binding => binding.DisplayName, StringComparer.Ordinal)
                        ],
                        Measures =
                        [
                            .. measuresOf[version.DatasetVersionId]
                                .OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal)
                                .Select(measure => new ManifestMeasure { Key = measure.MeasureKey, AppliesBridgeFactors = measure.BridgeFactorBehavior == BridgeFactorBehaviors.Value })
                        ]
                    })
                ]
            })
        ];
    }
}

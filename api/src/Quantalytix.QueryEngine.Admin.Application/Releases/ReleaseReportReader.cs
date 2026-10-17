using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Reads what a release's report pins need for its publication rules: each pinned report version with the content of the
/// model version it reads, and the fields, bindings, time bindings and measures of the dataset versions the release pins.
/// </summary>
public sealed class ReleaseReportReader(
    ITableGateway<ReleaseReportRecord> pins,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<VariableRecord> variables,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationRouteRecord> routes,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<BridgeVersionRecord> bridgeVersions)
{
    /// <summary>Reads a release's report pins and pinned dataset versions.</summary>
    /// <param name="releaseId">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned reports and dataset versions.</returns>
    public async Task<ReleaseReports> ReadAsync(long releaseId, CancellationToken cancellationToken)
    {
        var release = new { ReleaseId = releaseId };
        var pinned = await pins.ListAsync(release, cancellationToken);
        var reportKeys = (await reports.ListAsync(new { ReportId = pinned.Select(pin => pin.ReportId).ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        var pinnedReports = new List<PinnedReport>();
        foreach (var pin in pinned)
        {
            var version = await reportVersions.GetAsync(new { pin.ReportVersionId }, cancellationToken);
            var model = new { version.ModelVersionId };
            pinnedReports.Add(new PinnedReport
            {
                ReportKey = reportKeys[pin.ReportId],
                Version = version,
                Lines = await lines.ListAsync(new { pin.ReportVersionId }, cancellationToken),
                Variables = await variables.ListAsync(model, cancellationToken),
                Sources = [.. (await sources.ListAsync(model, cancellationToken)).Where(source => source.IsEnabled)],
                Dimensions = [.. (await dimensions.ListAsync(model, cancellationToken)).Where(dimension => dimension.IsEnabled)],
                Routes = await routes.ListAsync(model, cancellationToken)
            });
        }

        var datasetVersions = (await manifest.ListAsync(release, cancellationToken)).ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        long[] sourceDatasets = [.. pinnedReports.SelectMany(report => report.Sources.Select(source => source.DatasetId)).Distinct()];
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = sourceDatasets.Concat(datasetVersions.Keys).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = await bindings.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken);
        var timeIds = (await timeBindings.ListAsync(new { BindingId = versionBindings.Select(binding => binding.BindingId).ToArray() }, cancellationToken))
            .Select(extension => extension.BindingId).ToHashSet();
        var bridges = (await bridgeVersions.ListAsync(
                new { BridgeVersionId = versionBindings.Where(binding => binding.BridgeVersionId is not null).Select(binding => binding.BridgeVersionId!.Value).Distinct().ToArray() },
                cancellationToken))
            .ToDictionary(bridge => bridge.BridgeVersionId, bridge => (bridge.TargetDimensionVersionId, bridge.Cardinality));
        var versionFields = (await fields.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken)).ToLookup(field => field.DatasetVersionId);
        var versionMeasures = (await measures.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken)).ToLookup(measure => measure.DatasetVersionId);
        var bindingsByVersion = versionBindings.ToLookup(binding => binding.DatasetVersionId);
        return new ReleaseReports
        {
            Reports = [.. pinnedReports.OrderBy(report => report.ReportKey, StringComparer.Ordinal)],
            Datasets = datasetVersions.ToDictionary(pair => pair.Key, pair => new PinnedDatasetVersion
            {
                DatasetKey = datasetKeys[pair.Key],
                Fields = versionFields[pair.Value].ToDictionary(field => field.FieldKey, StringComparer.Ordinal),
                Bindings = [.. bindingsByVersion[pair.Value]],
                TimeBindingIds = bindingsByVersion[pair.Value].Where(binding => binding.BindingKind == BindingKinds.Time && timeIds.Contains(binding.BindingId))
                    .Select(binding => binding.BindingId).ToHashSet(),
                Measures = versionMeasures[pair.Value].ToDictionary(measure => measure.MeasureKey, StringComparer.Ordinal)
            }),
            DatasetKeys = datasetKeys,
            Bridges = bridges
        };
    }
}

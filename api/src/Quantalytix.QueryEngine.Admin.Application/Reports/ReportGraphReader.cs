using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads what the report rules check, by the kind being validated. A report version is checked against its model's published
/// version and the active release. A calculation model version is checked through every published report version that reads
/// its model, against the version itself and the active release, because publishing it makes it the version those reports
/// read. A semantic release is checked with every published report version against its own datasets, because a report runs
/// against whichever release is active.
/// </summary>
public sealed class ReportGraphReader(
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ModelMetricsReader models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<SourceProfileRecord> profiles,
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
    /// <summary>The key of the singleton <c>qe_database_info</c> row.</summary>
    private const int SingletonId = 1;

    /// <summary>Reads the report versions a rule checks, each with the model version it is checked against.</summary>
    /// <param name="kind">The kind being validated: <c>report_version</c>, <c>calculation_model_version</c> or <c>semantic_release</c>.</param>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graphs, by report key.</returns>
    public async Task<IReadOnlyList<ReportGraph>> ReportsAsync(string kind, long versionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportVersionRecord> scoped;
        CalculationModelVersionRecord? validatedModel = null;
        if (kind == VersionKinds.ReportVersion)
        {
            scoped = [await reportVersions.GetAsync(new { ReportVersionId = versionId }, cancellationToken)];
        }
        else if (kind == VersionKinds.CalculationModelVersion)
        {
            validatedModel = await modelVersions.GetAsync(new { ModelVersionId = versionId }, cancellationToken);
            scoped = await reportVersions.ListAsync(new { validatedModel.ModelId, Status = PublicationStatuses.Published }, cancellationToken);
        }
        else
        {
            scoped = await reportVersions.ListAsync(new { Status = PublicationStatuses.Published }, cancellationToken);
        }

        var reportKeys = (await reports.ListAsync(new { ReportId = scoped.Select(version => version.ReportId).ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        var graphs = new List<ReportGraph>();
        foreach (var version in scoped)
        {
            var model = validatedModel ?? await models.PublishedVersionAsync(version.ModelId, cancellationToken);
            graphs.Add(await GraphAsync(reportKeys[version.ReportId], version, model, cancellationToken));
        }

        return [.. graphs.OrderBy(graph => graph.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>Reads the report versions a rule checks, with the dataset versions of the release they run against.</summary>
    /// <param name="kind">The kind being validated: <c>report_version</c>, <c>calculation_model_version</c> or <c>semantic_release</c>.</param>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The reports, and the release's dataset versions: the validated release's, or the active release's.</returns>
    public async Task<ReportScope> ScopeAsync(string kind, long versionId, CancellationToken cancellationToken)
    {
        var graphs = await ReportsAsync(kind, versionId, cancellationToken);
        var releaseId = kind == VersionKinds.SemanticRelease ? versionId : (await databaseInfo.GetAsync(new { SingletonId }, cancellationToken)).ActiveReleaseId;
        var datasetVersions = (await manifest.ListAsync(new { ReleaseId = releaseId }, cancellationToken)).ToDictionary(row => row.DatasetId, row => row.DatasetVersionId);
        long[] sourceDatasets = [.. graphs.SelectMany(report => report.Sources.Select(source => source.DatasetId)).Distinct()];
        var datasetKeys = (await datasets.ListAsync(new { DatasetId = sourceDatasets.Concat(datasetVersions.Keys).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        var versionBindings = await bindings.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken);
        var timeIds = (await timeBindings.ListAsync(new { BindingId = versionBindings.Select(binding => binding.BindingId).ToArray() }, cancellationToken))
            .Select(extension => extension.BindingId).ToHashSet();
        var bridgeCardinalities = (await bridgeVersions.ListAsync(
                new { BridgeId = versionBindings.Where(binding => binding.BridgeId is not null).Select(binding => binding.BridgeId!.Value).Distinct().ToArray(), Status = PublicationStatuses.Published },
                cancellationToken))
            .ToDictionary(bridge => bridge.BridgeId, bridge => bridge.Cardinality);
        var versionFields = (await fields.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken)).ToLookup(field => field.DatasetVersionId);
        var versionMeasures = (await measures.ListAsync(new { DatasetVersionId = datasetVersions.Values.ToArray() }, cancellationToken)).ToLookup(measure => measure.DatasetVersionId);
        var bindingsByVersion = versionBindings.ToLookup(binding => binding.DatasetVersionId);
        return new ReportScope
        {
            Reports = graphs,
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
            BridgeCardinalities = bridgeCardinalities
        };
    }

    /// <summary>Reads a report version's columns and lines and the content of a model version.</summary>
    /// <param name="reportKey">The report's key.</param>
    /// <param name="version">The report version.</param>
    /// <param name="model">The model version the report is checked against; null while the model has no published version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The graph; with no model content when there is no model version.</returns>
    private async Task<ReportGraph> GraphAsync(string reportKey, ReportVersionRecord version, CalculationModelVersionRecord? model, CancellationToken cancellationToken)
    {
        var owner = new { version.ReportVersionId };
        var versionColumns = await columns.ListAsync(owner, cancellationToken);
        var versionLines = await lines.ListAsync(owner, cancellationToken);
        if (model is null)
        {
            return new ReportGraph
            {
                ReportKey = reportKey, Version = version, Columns = versionColumns, Lines = versionLines, Model = null,
                Variables = [], CalculatedMeasures = [], Profiles = [], Sources = [], Dimensions = [], Routes = []
            };
        }

        var content = new { model.ModelVersionId };
        return new ReportGraph
        {
            ReportKey = reportKey,
            Version = version,
            Columns = versionColumns,
            Lines = versionLines,
            Model = model,
            Variables = await variables.ListAsync(content, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(content, cancellationToken),
            Profiles = await profiles.ListAsync(content, cancellationToken),
            Sources = [.. (await sources.ListAsync(content, cancellationToken)).Where(source => source.IsEnabled)],
            Dimensions = [.. (await dimensions.ListAsync(content, cancellationToken)).Where(dimension => dimension.IsEnabled)],
            Routes = await routes.ListAsync(content, cancellationToken)
        };
    }
}

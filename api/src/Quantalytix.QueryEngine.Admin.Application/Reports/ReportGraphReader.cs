using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads what the report rules check, by the kind being validated. A report version is checked against its model's published
/// version and the active release. A calculation model version is checked through every published report version that reads
/// its model, against the version itself, because publishing it makes it the version those reports read. A semantic release is
/// checked with every published report version against its own datasets, because a report runs against whichever release is active.
/// </summary>
public sealed class ReportGraphReader(
    ITableGateway<DatabaseInfoRecord> databaseInfo,
    ITableGateway<ReleaseDatasetRecord> manifest,
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ModelMetricsReader models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<DimensionRoleRecord> roles)
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

        var reportsById = (await reports.ListAsync(new { ReportId = scoped.Select(version => version.ReportId).ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId);
        var graphs = new List<ReportGraph>();
        foreach (var version in scoped)
        {
            var model = validatedModel ?? await models.PublishedVersionAsync(version.ModelId, cancellationToken);
            graphs.Add(await GraphAsync(reportsById[version.ReportId], version, model, cancellationToken));
        }

        return [.. graphs.OrderBy(graph => graph.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>Reads the report versions a rule checks, with the dataset versions of the release they run against.</summary>
    /// <param name="kind">The kind being validated: <c>report_version</c> or <c>semantic_release</c>.</param>
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
        long[] namedBridges = [.. graphs.SelectMany(report => report.Sources.Select(source => source.BridgeId)).OfType<long>().Distinct()];
        long[] bridgedRoles = [.. graphs.SelectMany(report => report.Sources.Select(source => source.ClassificationRoleId)).OfType<long>().Distinct()];
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
            Bridges = (await bridges.ListAsync(new { BridgeId = namedBridges }, cancellationToken)).ToDictionary(bridge => bridge.BridgeId),
            RoleKeys = (await roles.ListAsync(new { RoleId = bridgedRoles }, cancellationToken)).ToDictionary(role => role.RoleId, role => role.RoleKey)
        };
    }

    /// <summary>Reads a report version's content and the metrics of a model version.</summary>
    /// <param name="report">The report.</param>
    /// <param name="version">The report version.</param>
    /// <param name="model">The model version the report is checked against; null while the model has no published version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The graph; with no metrics when there is no model version.</returns>
    private async Task<ReportGraph> GraphAsync(ReportRecord report, ReportVersionRecord version, CalculationModelVersionRecord? model, CancellationToken cancellationToken)
    {
        var owner = new { version.ReportVersionId };
        var versionProfiles = await profiles.ListAsync(owner, cancellationToken);
        var metrics = new { ModelVersionId = model?.ModelVersionId };
        return new ReportGraph
        {
            Report = report,
            Version = version,
            Columns = await columns.ListAsync(owner, cancellationToken),
            Lines = await lines.ListAsync(owner, cancellationToken),
            MemberOptions = await memberOptions.ListAsync(owner, cancellationToken),
            Scopes = await scopes.ListAsync(owner, cancellationToken),
            Profiles = versionProfiles,
            Picks = await picks.ListAsync(new { SourceProfileId = versionProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken),
            Sources = [.. (await sources.ListAsync(owner, cancellationToken)).Where(source => source.IsEnabled)],
            Dimensions = [.. (await dimensions.ListAsync(owner, cancellationToken)).Where(dimension => dimension.IsEnabled)],
            Model = model,
            Variables = model is null ? [] : await variables.ListAsync(metrics, cancellationToken),
            CalculatedMeasures = model is null ? [] : await calculatedMeasures.ListAsync(metrics, cancellationToken)
        };
    }
}

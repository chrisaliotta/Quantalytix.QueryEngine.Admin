using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads what the report rules check. A report version is checked against its snapshot: its model's version there, and the
/// snapshot's dataset and dimension versions; when a dataset version is committed, every enabled report of its snapshot that reads
/// the dataset is checked against the pending version.
/// </summary>
public sealed class ReportGraphReader(
    ReportVersionReader reports,
    OwnedVersions owned,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportStyleRecord> styles,
    ITableGateway<ReportRowParameterRecord> rowParameters,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportSourceLeftOutRecord> leftOut,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ModelParameterService parameters,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    SnapshotDatasets snapshotDatasets,
    SnapshotDimensionReader dimensionKeys)
{
    /// <summary>Reads a report version as it stands, with its model version's metrics and parameters.</summary>
    /// <param name="report">The report with its version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graph.</returns>
    public async Task<ReportGraph> ReadAsync(VersionedReport report, CancellationToken cancellationToken)
    {
        var version = report.Version;
        var owner = new { version.ReportVersionId };
        var structure = await owned.StructureAsync(report.Report, version.SnapshotId, cancellationToken);
        var reportProfiles = await profiles.ListAsync(owner, cancellationToken);
        var reportSources = await sources.ListAsync(owner, cancellationToken);
        long[] sourceIds = [.. reportSources.Select(source => source.ReportSourceId)];
        var modelVersion = (await modelVersions.ListAsync(new { version.CalculationModelId, version.SnapshotId }, cancellationToken)).Single();
        var metrics = new { modelVersion.CalculationModelVersionId };
        return new ReportGraph
        {
            Report = report.Report,
            Version = version,
            Members = await members.ListAsync(new { structure.DimensionVersionId }, cancellationToken),
            Lines = await lines.ListAsync(owner, cancellationToken),
            MemberOptions = await memberOptions.ListAsync(owner, cancellationToken),
            Styles = await styles.ListAsync(owner, cancellationToken),
            RowParameters = await rowParameters.ListAsync(owner, cancellationToken),
            Scopes = await scopes.ListAsync(owner, cancellationToken),
            Scenarios = await scenarios.ListAsync(owner, cancellationToken),
            Columns = [.. (await columns.ListAsync(owner, cancellationToken)).OrderBy(column => column.SortOrder)],
            Profiles = reportProfiles,
            Picks = await picks.ListAsync(new { CalculationSourceProfileId = reportProfiles.Select(profile => profile.CalculationSourceProfileId).ToArray() }, cancellationToken),
            Sources = reportSources,
            SourceVariables = await sourceVariables.ListAsync(new { ReportSourceId = sourceIds }, cancellationToken),
            SourceItems = await SourceItemsAsync(reportSources, version.SnapshotId, cancellationToken),
            LeftOut = await leftOut.ListAsync(new { ReportSourceId = sourceIds }, cancellationToken),
            Dimensions = await dimensions.ListAsync(owner, cancellationToken),
            ModelVersionId = modelVersion.CalculationModelVersionId,
            Variables = await variables.ListAsync(metrics, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(metrics, cancellationToken),
            Parameters = await parameters.ListAsync(modelVersion.CalculationModelVersionId, cancellationToken)
        };
    }

    /// <summary>Reads every enabled report version of a snapshot whose sources read a dataset.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graphs, by report key.</returns>
    public async Task<IReadOnlyList<ReportGraph>> EnabledReadingAsync(long snapshotId, long datasetId, CancellationToken cancellationToken)
    {
        var graphs = new List<ReportGraph>();
        foreach (var report in (await reports.InSnapshotAsync(snapshotId, null, cancellationToken)).Where(report => report.Version.IsEnabled))
        {
            var graph = await ReadAsync(report, cancellationToken);
            if (graph.Sources.Any(source => source.DatasetId == datasetId)) graphs.Add(graph);
        }

        return graphs;
    }

    /// <summary>Reads what reports are checked against besides their content, in a snapshot.</summary>
    /// <param name="graphs">The reports.</param>
    /// <param name="snapshotId">The reports' snapshot.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot's versions of the datasets the reports read, and the templates of the roles they name.</returns>
    public async Task<ReportScope> ScopeAsync(IReadOnlyList<ReportGraph> graphs, long snapshotId, CancellationToken cancellationToken)
    {
        var datasetsRead = DatasetsRead(graphs);
        return await ScopeAsync(graphs, await snapshotDatasets.ReadAsync(snapshotId, datasetsRead, cancellationToken), snapshotId, datasetsRead, cancellationToken);
    }

    /// <summary>Reads what reports are checked against in a snapshot, with one dataset's version replaced by a pending one.</summary>
    /// <param name="graphs">The reports.</param>
    /// <param name="snapshotId">The reports' snapshot.</param>
    /// <param name="datasetId">The dataset whose version is pending.</param>
    /// <param name="pending">The pending version, such as a dataset change set's result.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The scope with the pending version in place of the stored one.</returns>
    public async Task<ReportScope> PendingScopeAsync(
        IReadOnlyList<ReportGraph> graphs,
        long snapshotId,
        long datasetId,
        HeldDatasetVersion pending,
        CancellationToken cancellationToken)
    {
        var datasetsRead = DatasetsRead(graphs);
        var held = new Dictionary<long, HeldDatasetVersion>(await snapshotDatasets.ReadAsync(snapshotId, datasetsRead, cancellationToken)) { [datasetId] = pending };
        return await ScopeAsync(graphs, held, snapshotId, datasetsRead, cancellationToken);
    }

    /// <summary>The scope over some held dataset versions.</summary>
    private async Task<ReportScope> ScopeAsync(
        IReadOnlyList<ReportGraph> graphs,
        IReadOnlyDictionary<long, HeldDatasetVersion> held,
        long snapshotId,
        long[] datasetsRead,
        CancellationToken cancellationToken)
    {
        long[] namedRoles =
        [
            .. graphs.SelectMany(report => report.Sources.Select(source => source.DimensionRoleId).OfType<long>()
                .Concat(report.Dimensions.Select(dimension => dimension.DimensionRoleId))).Distinct()
        ];
        var rolesById = (await roles.ListAsync(new { DimensionRoleId = namedRoles }, cancellationToken)).ToDictionary(role => role.DimensionRoleId);
        var templates = new Dictionary<long, DimensionKeys?>();
        foreach (var role in rolesById.Values)
            templates[role.DimensionRoleId] = await dimensionKeys.ReadAsync(role.DimensionId, snapshotId, cancellationToken);
        return new ReportScope
        {
            Datasets = held,
            DatasetKeys = (await datasets.ListAsync(new { DatasetId = datasetsRead }, cancellationToken)).ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey),
            RoleKeys = rolesById.ToDictionary(pair => pair.Key, pair => pair.Value.DimensionRoleKey),
            Templates = templates
        };
    }

    /// <summary>The datasets the reports' sources read.</summary>
    private static long[] DatasetsRead(IReadOnlyList<ReportGraph> graphs)
    {
        return [.. graphs.SelectMany(report => report.Sources.Select(source => source.DatasetId)).Distinct()];
    }

    /// <summary>The rules of the bridge sources: the entries of each one's bridge version in the snapshot.</summary>
    private async Task<IReadOnlyList<ReportSourceItem>> SourceItemsAsync(IReadOnlyList<ReportSourceRecord> reportSources, long snapshotId, CancellationToken cancellationToken)
    {
        var sourceOf = (await owned.BridgesAsync(reportSources, snapshotId, cancellationToken)).ToDictionary(pair => pair.Value.BridgeVersionId, pair => pair.Key);
        var entries = await bridgeMappings.ListAsync(new { BridgeVersionId = sourceOf.Keys.ToArray() }, cancellationToken);
        return
        [
            .. entries.Select(entry => new ReportSourceItem
            {
                ReportSourceId = sourceOf[entry.BridgeVersionId],
                ItemKey = entry.SourceMemberKey,
                Selection = entry.SourceSelection,
                RowKey = entry.TargetMemberKey,
                Factor = entry.Factor
            })
        ];
    }
}

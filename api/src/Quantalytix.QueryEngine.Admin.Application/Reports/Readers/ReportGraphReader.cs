using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads what the report rules check. A report is checked against its model and the active release; when a semantic release is
/// validated, every enabled report is checked against its datasets, because a report runs against whichever release is active.
/// </summary>
public sealed class ReportGraphReader(
    ITableGateway<ReportRecord> reports,
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
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ModelParameterService parameters,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    ReleaseDatasets releaseDatasets,
    PublishedDimensionReader publishedDimensions)
{
    /// <summary>Reads a report as it stands, with its model's metrics and parameters.</summary>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graph.</returns>
    public async Task<ReportGraph> ReadAsync(ReportRecord report, CancellationToken cancellationToken)
    {
        var owner = new { report.ReportId };
        var structure = await owned.StructureAsync(report, cancellationToken);
        var reportProfiles = await profiles.ListAsync(owner, cancellationToken);
        var reportSources = await sources.ListAsync(owner, cancellationToken);
        var metrics = new { report.CalculationModelId };
        return new ReportGraph
        {
            Report = report,
            Members = await members.ListAsync(new { structure.DimensionVersionId }, cancellationToken),
            Lines = await lines.ListAsync(owner, cancellationToken),
            MemberOptions = await memberOptions.ListAsync(owner, cancellationToken),
            Styles = await styles.ListAsync(owner, cancellationToken),
            RowParameters = await rowParameters.ListAsync(owner, cancellationToken),
            Scopes = await scopes.ListAsync(owner, cancellationToken),
            Scenarios = await scenarios.ListAsync(owner, cancellationToken),
            Columns = await columns.ListAsync(owner, cancellationToken),
            Profiles = reportProfiles,
            Picks = await picks.ListAsync(new { CalculationSourceProfileId = reportProfiles.Select(profile => profile.CalculationSourceProfileId).ToArray() }, cancellationToken),
            Sources = reportSources,
            SourceVariables = await sourceVariables.ListAsync(new { ReportSourceId = reportSources.Select(source => source.ReportSourceId).ToArray() }, cancellationToken),
            SourceItems = await SourceItemsAsync(reportSources, cancellationToken),
            Dimensions = await dimensions.ListAsync(owner, cancellationToken),
            Variables = await variables.ListAsync(metrics, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(metrics, cancellationToken),
            Parameters = await parameters.ListAsync(report.CalculationModelId, cancellationToken)
        };
    }

    /// <summary>Reads every enabled report, which a release being validated serves.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graphs, by report key.</returns>
    public async Task<IReadOnlyList<ReportGraph>> EnabledAsync(CancellationToken cancellationToken)
    {
        var graphs = new List<ReportGraph>();
        foreach (var report in (await reports.ListAsync(null, cancellationToken)).Where(report => report.IsEnabled))
            graphs.Add(await ReadAsync(report, cancellationToken));
        return [.. graphs.OrderBy(graph => graph.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>Reads what reports are checked against besides their content.</summary>
    /// <param name="graphs">The reports.</param>
    /// <param name="semanticReleaseId">The release being validated; null for the active release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The release's versions of the datasets the reports read, and the templates of their calculation dimensions.</returns>
    public async Task<ReportScope> ScopeAsync(IReadOnlyList<ReportGraph> graphs, long? semanticReleaseId, CancellationToken cancellationToken)
    {
        long[] sourceDatasets = [.. graphs.SelectMany(report => report.Sources.Select(source => source.DatasetId)).Distinct()];
        long[] calculationRoles = [.. graphs.SelectMany(report => report.Dimensions.Select(dimension => dimension.DimensionRoleId)).Distinct()];
        long[] namedRoles = [.. graphs.SelectMany(report => report.Sources.Select(source => source.DimensionRoleId)).OfType<long>().Concat(calculationRoles).Distinct()];
        var rolesById = (await roles.ListAsync(new { DimensionRoleId = namedRoles }, cancellationToken)).ToDictionary(role => role.DimensionRoleId);
        var templates = new Dictionary<long, DimensionKeys?>();
        foreach (var dimensionRoleId in calculationRoles)
            templates[dimensionRoleId] = await publishedDimensions.ReadAsync(rolesById[dimensionRoleId].DimensionId, cancellationToken);
        return new ReportScope
        {
            Datasets = await releaseDatasets.ReadAsync(semanticReleaseId, sourceDatasets, cancellationToken),
            DatasetKeys = (await datasets.ListAsync(new { DatasetId = sourceDatasets }, cancellationToken)).ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey),
            RoleKeys = rolesById.ToDictionary(pair => pair.Key, pair => pair.Value.DimensionRoleKey),
            Templates = templates
        };
    }

    /// <summary>The rules of the bridge sources: the entries of each one's bridge.</summary>
    private async Task<IReadOnlyList<ReportSourceItem>> SourceItemsAsync(IReadOnlyList<ReportSourceRecord> reportSources, CancellationToken cancellationToken)
    {
        var sourceOf = (await owned.BridgesAsync(reportSources, cancellationToken)).ToDictionary(pair => pair.Value.BridgeVersionId, pair => pair.Key);
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

using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads what the report rules check. A report is checked against its model's published version and the active release. When a
/// calculation model version is validated, every enabled report of its model is checked against the version itself, because
/// publishing it makes it the version those reports read; when a semantic release is validated, every enabled report is checked
/// against its datasets, because a report runs against whichever release is active.
/// </summary>
public sealed class ReportGraphReader(
    ITableGateway<ReportRecord> reports,
    OwnedVersions owned,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportMemberOptionRecord> memberOptions,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ModelMetricsReader models,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DimensionRoleRecord> roles,
    ReleaseDatasets releaseDatasets,
    PublishedDimensionReader publishedDimensions)
{
    /// <summary>Reads a report as it stands, with its model's published version.</summary>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graph.</returns>
    public async Task<ReportGraph> ReadAsync(ReportRecord report, CancellationToken cancellationToken)
    {
        return await GraphAsync(report, await models.PublishedVersionAsync(report.ModelId, cancellationToken), cancellationToken);
    }

    /// <summary>Reads the enabled reports a model version or a release being validated serves.</summary>
    /// <param name="kind">The kind being validated: <c>calculation_model_version</c> or <c>semantic_release</c>.</param>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report graphs: a model version's with the version itself, a release's with their models' published versions.</returns>
    public async Task<IReadOnlyList<ReportGraph>> ConsumersAsync(string kind, long versionId, CancellationToken cancellationToken)
    {
        var graphs = new List<ReportGraph>();
        if (kind == VersionKinds.CalculationModelVersion)
        {
            var model = await modelVersions.GetAsync(new { ModelVersionId = versionId }, cancellationToken);
            foreach (var report in (await reports.ListAsync(new { model.ModelId }, cancellationToken)).Where(report => report.IsEnabled))
                graphs.Add(await GraphAsync(report, model, cancellationToken));
        }
        else
        {
            foreach (var report in (await reports.ListAsync(null, cancellationToken)).Where(report => report.IsEnabled))
                graphs.Add(await ReadAsync(report, cancellationToken));
        }
        return [.. graphs.OrderBy(graph => graph.ReportKey, StringComparer.Ordinal)];
    }

    /// <summary>Reads what reports are checked against besides their content.</summary>
    /// <param name="graphs">The reports.</param>
    /// <param name="releaseId">The release being validated; null for the active release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The release's versions of the datasets the reports read, and the templates of their calculation dimensions.</returns>
    public async Task<ReportScope> ScopeAsync(IReadOnlyList<ReportGraph> graphs, long? releaseId, CancellationToken cancellationToken)
    {
        long[] sourceDatasets = [.. graphs.SelectMany(report => report.Sources.Select(source => source.DatasetId)).Distinct()];
        long[] calculationRoles = [.. graphs.SelectMany(report => report.Dimensions.Select(dimension => dimension.RoleId)).Distinct()];
        long[] namedRoles = [.. graphs.SelectMany(report => report.Sources.Select(source => source.RoleId)).OfType<long>().Concat(calculationRoles).Distinct()];
        var rolesById = (await roles.ListAsync(new { RoleId = namedRoles }, cancellationToken)).ToDictionary(role => role.RoleId);
        var templates = new Dictionary<long, DimensionKeys?>();
        foreach (var roleId in calculationRoles)
            templates[roleId] = await publishedDimensions.ReadAsync(rolesById[roleId].DimensionId, cancellationToken);
        return new ReportScope
        {
            Datasets = await releaseDatasets.ReadAsync(releaseId, sourceDatasets, cancellationToken),
            DatasetKeys = (await datasets.ListAsync(new { DatasetId = sourceDatasets }, cancellationToken)).ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey),
            RoleKeys = rolesById.ToDictionary(pair => pair.Key, pair => pair.Value.RoleKey),
            Templates = templates
        };
    }

    /// <summary>Reads a report's content and the metrics of a model version.</summary>
    /// <param name="report">The report.</param>
    /// <param name="model">The model version the report is checked against.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The graph.</returns>
    private async Task<ReportGraph> GraphAsync(ReportRecord report, CalculationModelVersionRecord model, CancellationToken cancellationToken)
    {
        var owner = new { report.ReportId };
        var structure = await owned.StructureAsync(report, cancellationToken);
        var reportProfiles = await profiles.ListAsync(owner, cancellationToken);
        var reportSources = await sources.ListAsync(owner, cancellationToken);
        var metrics = new { model.ModelVersionId };
        return new ReportGraph
        {
            Report = report,
            Members = await members.ListAsync(new { structure.DimensionVersionId }, cancellationToken),
            Lines = await lines.ListAsync(owner, cancellationToken),
            MemberOptions = await memberOptions.ListAsync(owner, cancellationToken),
            Scopes = await scopes.ListAsync(owner, cancellationToken),
            Columns = await columns.ListAsync(owner, cancellationToken),
            Profiles = reportProfiles,
            Picks = await picks.ListAsync(new { SourceProfileId = reportProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken),
            Sources = reportSources,
            SourceVariables = await sourceVariables.ListAsync(new { ReportSourceId = reportSources.Select(source => source.ReportSourceId).ToArray() }, cancellationToken),
            Dimensions = await dimensions.ListAsync(owner, cancellationToken),
            Model = model,
            Variables = await variables.ListAsync(metrics, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(metrics, cancellationToken)
        };
    }
}

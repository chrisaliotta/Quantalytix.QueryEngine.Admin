using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the version of a calculation model that its reports are checked against: its published version, which the active
/// assumption set pins and every run that names no set reads.
/// </summary>
public sealed class ModelMetricsReader(
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures)
{
    /// <summary>A model's published version.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published version, or null while the model has none.</returns>
    public async Task<CalculationModelVersionRecord?> PublishedVersionAsync(long modelId, CancellationToken cancellationToken)
    {
        return (await modelVersions.ListAsync(new { ModelId = modelId, Status = PublicationStatuses.Published }, cancellationToken)).SingleOrDefault();
    }

    /// <summary>The published version of the model a report version reads.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The model's published version, or null while it has none.</returns>
    public async Task<CalculationModelVersionRecord?> ForReportAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var version = await reportVersions.GetAsync(new { ReportVersionId = reportVersionId }, cancellationToken);
        return await PublishedVersionAsync(version.ModelId, cancellationToken);
    }

    /// <summary>The metric keys of the published version of the model a report version reads.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The variable and calculated measure keys; none while the model has no published version.</returns>
    public async Task<ModelMetrics> ReadForReportAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        if (await ForReportAsync(reportVersionId, cancellationToken) is not { } model) return ModelMetrics.None;
        var owner = new { model.ModelVersionId };
        return new ModelMetrics
        {
            VariableKeys = (await variables.ListAsync(owner, cancellationToken)).Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal),
            CalculatedMeasureKeys = (await calculatedMeasures.ListAsync(owner, cancellationToken)).Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal)
        };
    }
}

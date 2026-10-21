using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the version of a calculation model that its reports are checked against: its published version, which the active
/// assumption set pins and every run reads. A report's model has one from the report's creation on, because a living version is
/// only ever replaced by its successor.
/// </summary>
public sealed class ModelMetricsReader(
    ITableGateway<ReportRecord> reports,
    ITableGateway<CalculationModelVersionRecord> modelVersions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures)
{
    /// <summary>The published version of a report's model.</summary>
    /// <param name="modelId">The calculation model a report reads.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published version.</returns>
    public async Task<CalculationModelVersionRecord> PublishedVersionAsync(long modelId, CancellationToken cancellationToken)
    {
        return (await modelVersions.ListAsync(new { ModelId = modelId, Status = PublicationStatuses.Published }, cancellationToken)).Single();
    }

    /// <summary>The metric keys of the published version of the model a report reads.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The variable and calculated measure keys.</returns>
    public async Task<ModelMetrics> ReadForReportAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        var owner = new { (await PublishedVersionAsync(report.ModelId, cancellationToken)).ModelVersionId };
        return new ModelMetrics
        {
            VariableKeys = (await variables.ListAsync(owner, cancellationToken)).Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal),
            CalculatedMeasureKeys = (await calculatedMeasures.ListAsync(owner, cancellationToken)).Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal)
        };
    }
}

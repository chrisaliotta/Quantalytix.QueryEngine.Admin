using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads the metric keys of the calculation model a report reads, which report content names by key.</summary>
public sealed class ModelMetricsReader(
    ITableGateway<ReportRecord> reports,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures)
{
    /// <summary>The metric keys of the model a report reads.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The variable and calculated measure keys.</returns>
    public async Task<ModelMetrics> ReadForReportAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        var owner = new { report.ModelId };
        return new ModelMetrics
        {
            VariableKeys = (await variables.ListAsync(owner, cancellationToken)).Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal),
            CalculatedMeasureKeys = (await calculatedMeasures.ListAsync(owner, cancellationToken)).Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal)
        };
    }
}

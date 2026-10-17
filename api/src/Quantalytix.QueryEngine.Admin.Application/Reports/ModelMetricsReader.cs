using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads the metrics of a model version, or of the model version a report version reads.</summary>
public sealed class ModelMetricsReader(
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures)
{
    /// <summary>Reads a model version's metrics.</summary>
    /// <param name="modelVersionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Its variables and calculated measures.</returns>
    public async Task<ModelMetrics> ReadAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var owner = new { ModelVersionId = modelVersionId };
        return new ModelMetrics
        {
            ModelVersionId = modelVersionId,
            Variables = await variables.ListAsync(owner, cancellationToken),
            CalculatedMeasures = await calculatedMeasures.ListAsync(owner, cancellationToken)
        };
    }

    /// <summary>Reads the metrics of the model version a report version reads.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Its model version's variables and calculated measures.</returns>
    public async Task<ModelMetrics> ReadForReportAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var version = await reportVersions.GetAsync(new { ReportVersionId = reportVersionId }, cancellationToken);
        return await ReadAsync(version.ModelVersionId!.Value, cancellationToken);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the report rows that use a model. Report versions of any status name the model itself. Published report versions name its
/// metrics by key, resolved in the model's published version: columns return variables and calculated measures, formula rows
/// evaluate calculated measures, and sources and targets name variables, which calculated measures read through their formulas.
/// Deleting such a metric from the published version would leave them unresolved; a draft's metrics are used by no report.
/// </summary>
public sealed class ReportUses(
    ReportVersionNames names,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<ReportVariableTargetRecord> targets,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationModelVersionRecord> modelVersions)
{
    /// <summary>The report versions of any status that read a model.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version, like <c>'bank.margin_statement' v2</c>.</returns>
    public async Task<IEnumerable<string>> ModelReadersAsync(long modelId, CancellationToken cancellationToken)
    {
        var readers = await names.OfAsync(await versions.ListAsync(new { ModelId = modelId }, cancellationToken), cancellationToken);
        return readers.Values;
    }

    /// <summary>
    /// The rows of live reports that read a variable: columns that return it, sources that supply it, targets on it, and the
    /// columns and formula rows whose calculated measures read it.
    /// </summary>
    /// <param name="variable">A variable of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report row.</returns>
    public async Task<IEnumerable<string>> VariableUsesAsync(VariableRecord variable, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(variable.ModelVersionId, cancellationToken);
        long[] readerIds = [.. readers.Keys];
        var key = variable.VariableKey;
        var owner = new { variable.ModelVersionId };
        var graph = new FormulaGraph(await variables.ListAsync(owner, cancellationToken), await calculatedMeasures.ListAsync(owner, cancellationToken));
        string[] reading = [.. graph.MeasureKeys.Where(measure => graph.Variables(measure).Contains(key))];
        var returning = await columns.ListAsync(new { ReportVersionId = readerIds, ValueKind = ReportValueKinds.Variable, MetricKey = key }, cancellationToken);
        var computing = await columns.ListAsync(new { ReportVersionId = readerIds, ValueKind = ReportValueKinds.Calculated, MetricKey = reading }, cancellationToken);
        var evaluating = await lines.ListAsync(new { ReportVersionId = readerIds, CalculatedMeasureKey = reading }, cancellationToken);
        var supplying = await sources.ListAsync(new { ReportVersionId = readerIds, VariableKey = key }, cancellationToken);
        var targeting = await targets.ListAsync(new { ReportVersionId = readerIds, VariableKey = key }, cancellationToken);
        return
        [
            .. returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportVersionId]}"),
            .. computing.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportVersionId]}, through '{column.MetricKey}'"),
            .. evaluating.Select(line => $"row '{line.LineKey}' of {readers[line.ReportVersionId]}, through '{line.CalculatedMeasureKey}'"),
            .. supplying.Select(source => $"source '{source.SourceKey}' of {readers[source.ReportVersionId]}"),
            .. targeting.Select(target => $"the variable target of {readers[target.ReportVersionId]}")
        ];
    }

    /// <summary>The columns and formula rows of live reports that read a calculated measure.</summary>
    /// <param name="measure">A calculated measure of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column or row.</returns>
    public async Task<IEnumerable<string>> MeasureUsesAsync(CalculatedMeasureRecord measure, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(measure.ModelVersionId, cancellationToken);
        long[] readerIds = [.. readers.Keys];
        var returning = await columns.ListAsync(
            new { ReportVersionId = readerIds, ValueKind = ReportValueKinds.Calculated, MetricKey = measure.MeasureKey },
            cancellationToken);
        var evaluating = await lines.ListAsync(new { ReportVersionId = readerIds, CalculatedMeasureKey = measure.MeasureKey }, cancellationToken);
        return
        [
            .. returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportVersionId]}"),
            .. evaluating.Select(line => $"row '{line.LineKey}' of {readers[line.ReportVersionId]}")
        ];
    }

    /// <summary>
    /// The published report versions that read a model version's model, by ID with their names, when the model version is
    /// published; none otherwise, because reports read the published version.
    /// </summary>
    private async Task<IReadOnlyDictionary<long, string>> LiveReadersAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var model = await modelVersions.GetAsync(new { ModelVersionId = modelVersionId }, cancellationToken);
        if (model.Status != PublicationStatuses.Published) return new Dictionary<long, string>();
        return await names.OfAsync(await versions.ListAsync(new { model.ModelId, Status = PublicationStatuses.Published }, cancellationToken), cancellationToken);
    }
}

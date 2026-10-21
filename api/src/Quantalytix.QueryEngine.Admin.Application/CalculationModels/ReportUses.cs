using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Names the report rows that use a model. Every report of the model names the model itself. Enabled reports name its metrics by
/// key, resolved in the model's published version: columns return variables and calculated measures, formula rows evaluate
/// calculated measures, and source variables and scopes name variables, which calculated measures read through their formulas.
/// Deleting such a metric from the published version would leave them unresolved; a draft's metrics are used by no report.
/// </summary>
public sealed class ReportUses(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<ReportVariableScopeRecord> scopes,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationModelVersionRecord> modelVersions)
{
    /// <summary>The reports, enabled or not, that read a model.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One name per report, like <c>report 'bank.margin_statement'</c>.</returns>
    public async Task<IEnumerable<string>> ModelReadersAsync(long modelId, CancellationToken cancellationToken)
    {
        return ReportNames.Of(await reports.ListAsync(new { ModelId = modelId }, cancellationToken)).Values;
    }

    /// <summary>
    /// The rows of enabled reports that read a variable: columns that return it, sources that supply it, scopes on it, and the
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
        var returning = await columns.ListAsync(new { ReportId = readerIds, ValueKind = ReportValueKinds.Variable, MetricKey = key }, cancellationToken);
        var computing = await columns.ListAsync(new { ReportId = readerIds, ValueKind = ReportValueKinds.Calculated, MetricKey = reading }, cancellationToken);
        var evaluating = await lines.ListAsync(new { ReportId = readerIds, CalculatedMeasureKey = reading }, cancellationToken);
        var readerSources = (await sources.ListAsync(new { ReportId = readerIds }, cancellationToken)).ToDictionary(source => source.ReportSourceId);
        var supplying = await sourceVariables.ListAsync(new { ReportSourceId = readerSources.Keys.ToArray(), VariableKey = key }, cancellationToken);
        var scoping = await scopes.ListAsync(new { ReportId = readerIds, VariableKey = key }, cancellationToken);
        return
        [
            .. returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportId]}"),
            .. computing.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportId]}, through '{column.MetricKey}'"),
            .. evaluating.Select(line => $"row '{line.LineKey}' of {readers[line.ReportId]}, through '{line.CalculatedMeasureKey}'"),
            .. supplying.Select(supplied => readerSources[supplied.ReportSourceId])
                .Select(source => $"source '{source.SourceKey}' of {readers[source.ReportId]}"),
            .. scoping.Select(scope => $"the variable scope of {readers[scope.ReportId]}")
        ];
    }

    /// <summary>The columns and formula rows of enabled reports that read a calculated measure.</summary>
    /// <param name="measure">A calculated measure of a model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per column or row.</returns>
    public async Task<IEnumerable<string>> MeasureUsesAsync(CalculatedMeasureRecord measure, CancellationToken cancellationToken)
    {
        var readers = await LiveReadersAsync(measure.ModelVersionId, cancellationToken);
        long[] readerIds = [.. readers.Keys];
        var returning = await columns.ListAsync(
            new { ReportId = readerIds, ValueKind = ReportValueKinds.Calculated, MetricKey = measure.MeasureKey },
            cancellationToken);
        var evaluating = await lines.ListAsync(new { ReportId = readerIds, CalculatedMeasureKey = measure.MeasureKey }, cancellationToken);
        return
        [
            .. returning.Select(column => $"column '{column.ColumnKey}' of {readers[column.ReportId]}"),
            .. evaluating.Select(line => $"row '{line.LineKey}' of {readers[line.ReportId]}")
        ];
    }

    /// <summary>
    /// The enabled reports that read a model version's model, by ID with their names, when the model version is published; none
    /// otherwise, because reports read the published version.
    /// </summary>
    private async Task<IReadOnlyDictionary<long, string>> LiveReadersAsync(long modelVersionId, CancellationToken cancellationToken)
    {
        var model = await modelVersions.GetAsync(new { ModelVersionId = modelVersionId }, cancellationToken);
        if (model.Status != PublicationStatuses.Published) return new Dictionary<long, string>();
        return ReportNames.Of((await reports.ListAsync(new { model.ModelId }, cancellationToken)).Where(report => report.IsEnabled));
    }
}

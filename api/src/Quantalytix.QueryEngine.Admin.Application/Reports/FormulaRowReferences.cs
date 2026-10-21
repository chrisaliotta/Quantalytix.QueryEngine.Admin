using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks what formula rows of a report name: a calculated measure of the published version of the report's model, which the row
/// evaluates, and one of the report's columns, which the row places its value in.
/// </summary>
/// <param name="metrics">Reads the metrics of the published version of the report's model.</param>
/// <param name="columns">Reads the report's columns.</param>
public sealed class FormulaRowReferences(ModelMetricsReader metrics, ITableGateway<ReportColumnRecord> columns)
{
    /// <summary>The problems of formula rows' references; none when every reference resolves.</summary>
    /// <param name="reportId">The report the rows belong to.</param>
    /// <param name="rows">Each formula row's key, calculated measure key and value column key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One message per reference that does not resolve.</returns>
    public async Task<IReadOnlyList<string>> ProblemsAsync(
        long reportId,
        IEnumerable<(string RowKey, string? MeasureKey, string? ColumnKey)> rows,
        CancellationToken cancellationToken)
    {
        var model = await metrics.ReadForReportAsync(reportId, cancellationToken);
        var columnKeys = (await columns.ListAsync(new { ReportId = reportId }, cancellationToken))
            .Select(column => column.ColumnKey)
            .ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var (rowKey, measureKey, columnKey) in rows)
        {
            if (measureKey is not null && !model.Has(ReportValueKinds.Calculated, measureKey))
                problems.Add($"Formula row '{rowKey}' evaluates '{measureKey}', which is no calculated measure of the published version of the report's model.");
            if (columnKey is not null && !columnKeys.Contains(columnKey))
                problems.Add($"Formula row '{rowKey}' places its value in '{columnKey}', which is no column of this report.");
        }
        return problems;
    }
}

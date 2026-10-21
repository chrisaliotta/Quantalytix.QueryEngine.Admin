using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the ordered column lists of reports.</summary>
public sealed class ReportColumnService(ITableGateway<ReportColumnRecord> columns, ReportChanges changes, IIdGenerator ids)
{
    /// <summary>Lists a report's columns.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The columns in their order.</returns>
    public async Task<IReadOnlyList<ReportColumnRecord>> ListAsync(long reportId, CancellationToken cancellationToken)
    {
        return [.. (await columns.ListAsync(new { ReportId = reportId }, cancellationToken)).OrderBy(column => column.SortOrder)];
    }

    /// <summary>
    /// Replaces a report's columns with the given list, in its order. A column that carries its ID keeps it, so the formula rows
    /// that place values in it keep pointing at it; columns the list leaves out are deleted.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete, validated column list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the list, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, ReportColumnsInput input, CancellationToken cancellationToken)
    {
        ReportColumnRecord[] rows =
        [
            .. input.Columns.Select((column, index) => new ReportColumnRecord
            {
                ReportColumnId = column.ReportColumnId ?? ids.NextId(),
                ReportId = reportId,
                ColumnKey = column.ColumnKey,
                DisplayName = column.DisplayName,
                ValueKind = column.ValueKind,
                MetricKey = column.MetricKey,
                SortOrder = index + 1,
                DisplayFormat = column.DisplayFormat,
                IsVisible = column.IsVisible,
                PayloadJson = column.PayloadJson
            })
        ];
        return await changes.ApplyAsync(reportId, report => report with { Columns = rows }, async () =>
        {
            var removed = (await columns.ListAsync(new { ReportId = reportId }, cancellationToken))
                .Select(column => column.ReportColumnId).Except(rows.Select(row => row.ReportColumnId)).ToArray();
            await columns.DeleteAsync(new { ReportColumnId = removed }, cancellationToken);
            await columns.InsertManyAsync(rows, cancellationToken);
        }, cancellationToken);
    }
}

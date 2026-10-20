using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the ordered column list of report versions.</summary>
public sealed class ReportColumnService(ITableGateway<ReportColumnRecord> columns, IIdGenerator ids)
{
    /// <summary>Lists a version's columns.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The columns in their order.</returns>
    public async Task<IReadOnlyList<ReportColumnRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        return [.. (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken)).OrderBy(column => column.SortOrder)];
    }

    /// <summary>
    /// Replaces a version's columns with the given list, in its order. A column that carries its ID keeps it, so the formula
    /// rows that place values in it keep pointing at it; columns the list leaves out are deleted.
    /// </summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete, validated column list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored columns in their order.</returns>
    public async Task<IReadOnlyList<ReportColumnRecord>> ReplaceAsync(long versionId, ReportColumnsInput input, CancellationToken cancellationToken)
    {
        ReportColumnRecord[] rows =
        [
            .. input.Columns.Select((column, index) => new ReportColumnRecord
            {
                ReportColumnId = column.ReportColumnId ?? ids.NextId(),
                ReportVersionId = versionId,
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
        var removed = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken))
            .Select(column => column.ReportColumnId).Except(rows.Select(row => row.ReportColumnId)).ToArray();
        await columns.DeleteAsync(new { ReportColumnId = removed }, cancellationToken);
        await columns.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the overlay rows of report versions.</summary>
public sealed class ReportLineService(
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    IIdGenerator ids)
{
    /// <summary>Lists a version's overlay rows.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows by sort order, then key.</returns>
    public async Task<IReadOnlyList<ReportLineRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(line => line.SortOrder).ThenBy(line => line.LineKey, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Replaces a version's overlay rows with the given list. A row that carries its ID keeps it, rows the list leaves out are
    /// deleted, and value columns become the IDs the rows store, by column key. Parent, member and calculated measure keys are
    /// stored as keys.
    /// </summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete, validated overlay rows.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored rows by sort order, then key.</returns>
    public async Task<IReadOnlyList<ReportLineRecord>> ReplaceAsync(long versionId, ReportLinesInput input, CancellationToken cancellationToken)
    {
        var columnIds = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken))
            .ToDictionary(column => column.ColumnKey, column => column.ReportColumnId, StringComparer.Ordinal);
        ReportLineRecord[] rows =
        [
            .. input.Lines.Select(line => new ReportLineRecord
            {
                ReportLineId = line.ReportLineId ?? ids.NextId(),
                ReportVersionId = versionId,
                LineKey = line.LineKey,
                DisplayName = line.DisplayName,
                LineKind = line.LineKind,
                ParentKey = line.ParentKey,
                CalculatedMeasureKey = line.CalculatedMeasureKey,
                ValueColumnId = line.ValueColumnKey is { } column ? columnIds[column] : null,
                SortOrder = line.SortOrder,
                IsVisible = line.IsVisible,
                PayloadJson = line.PayloadJson,
                SettingsJson = line.SettingsJson
            })
        ];
        var removed = (await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken))
            .Select(line => line.ReportLineId).Except(rows.Select(row => row.ReportLineId)).ToArray();
        await lines.DeleteAsync(new { ReportLineId = removed }, cancellationToken);
        await lines.InsertManyAsync(rows, cancellationToken);
        return await ListAsync(versionId, cancellationToken);
    }
}

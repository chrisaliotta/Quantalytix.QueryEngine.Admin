using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the line tree of report versions.</summary>
public sealed class ReportLineService(
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReportColumnRecord> columns,
    ModelMetricsReader metrics,
    IIdGenerator ids)
{
    /// <summary>Lists a version's lines.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines by parent, then sort order.</returns>
    public async Task<IReadOnlyList<ReportLineRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(line => line.ParentReportLineId ?? 0).ThenBy(line => line.SortOrder)];
    }

    /// <summary>
    /// Replaces a draft version's line tree with the given pre-order list. Each line's sort order is its position among its
    /// siblings; a line that carries its ID keeps it, lines the list leaves out are deleted, and keys become the IDs the rows
    /// store: parents by line key, formula measures by key in the report's model version, value columns by column key.
    /// </summary>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="input">The complete, validated line tree.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored lines by parent, then sort order.</returns>
    public async Task<IReadOnlyList<ReportLineRecord>> ReplaceAsync(long versionId, ReportLinesInput input, CancellationToken cancellationToken)
    {
        var model = await metrics.ReadForReportAsync(versionId, cancellationToken);
        var columnIds = (await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken))
            .ToDictionary(column => column.ColumnKey, column => column.ReportColumnId, StringComparer.Ordinal);
        var lineIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var siblings = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<ReportLineRecord>(input.Lines.Count);
        foreach (var line in input.Lines)
        {
            var id = line.ReportLineId ?? ids.NextId();
            lineIds[line.LineKey] = id;
            var siblingKey = line.ParentLineKey ?? string.Empty;
            siblings[siblingKey] = siblings.GetValueOrDefault(siblingKey) + 1;
            rows.Add(new ReportLineRecord
            {
                ReportLineId = id,
                ReportVersionId = versionId,
                LineKey = line.LineKey,
                DisplayName = line.DisplayName,
                LineKind = line.LineKind,
                ParentReportLineId = line.ParentLineKey is { } parent ? lineIds[parent] : null,
                ReportMemberKey = line.ReportMemberKey,
                CalculatedMeasureId = line.CalculatedMeasureKey is { } measure ? model.IdOf(ReportValueKinds.Calculated, measure) : null,
                ValueColumnId = line.ValueColumnKey is { } column ? columnIds[column] : null,
                SortOrder = siblings[siblingKey],
                IsVisible = line.IsVisible,
                PayloadJson = line.PayloadJson,
                SettingsJson = line.SettingsJson
            });
        }

        var removed = (await lines.ListAsync(new { ReportVersionId = versionId }, cancellationToken))
            .Select(line => line.ReportLineId).Except(rows.Select(row => row.ReportLineId)).ToArray();
        await lines.DeleteAsync(new { ReportLineId = removed }, cancellationToken);
        await lines.InsertManyAsync(rows, cancellationToken);
        return await ListAsync(versionId, cancellationToken);
    }
}

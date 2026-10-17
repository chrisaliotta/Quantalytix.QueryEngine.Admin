using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Copies a report version's columns and lines into a new draft that may read another model version. Each column follows its
/// metric key to the metric with the same key in the draft's model version, and each formula line its calculated measure key;
/// a column whose metric the new model lacks is dropped, and so is a formula line whose measure or column is gone. A dropped
/// line's children move up to its parent, so the tree stays whole.
/// </summary>
public sealed class ReportVersionCloner(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ModelMetricsReader metrics,
    IIdGenerator ids)
{
    /// <summary>Copies the source's columns and lines into the target, remapped by key.</summary>
    /// <param name="sourceVersionId">The report version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and inserts.</param>
    /// <returns>The draft with the keys of the columns and lines the clone dropped.</returns>
    public async Task<CreatedReportVersion> CloneAsync(long sourceVersionId, ReportVersionRecord target, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { ReportVersionId = sourceVersionId }, cancellationToken);
        var from = await metrics.ReadAsync(source.ModelVersionId!.Value, cancellationToken);
        var to = await metrics.ReadAsync(target.ModelVersionId!.Value, cancellationToken);
        var remap = new IdRemap(ids);

        var cloned = new List<ReportColumnRecord>();
        var droppedColumns = new List<string>();
        foreach (var column in (await columns.ListAsync(new { ReportVersionId = sourceVersionId }, cancellationToken)).OrderBy(column => column.SortOrder))
        {
            if (to.IdOf(column.ValueKind, from.KeyOf(column)) is not { } metricId)
            {
                droppedColumns.Add(column.ColumnKey);
                continue;
            }

            var variable = column.ValueKind == ReportValueKinds.Variable;
            cloned.Add(column with
            {
                ReportColumnId = remap.Map(column.ReportColumnId),
                ReportVersionId = target.ReportVersionId,
                VariableId = variable ? metricId : null,
                CalculatedMeasureId = variable ? null : metricId
            });
        }

        var sourceLines = (await lines.ListAsync(new { ReportVersionId = sourceVersionId }, cancellationToken)).ToDictionary(line => line.ReportLineId);
        var dropped = new HashSet<long>();
        var kept = new List<(ReportLineRecord Source, long? MeasureId)>();
        foreach (var line in sourceLines.Values)
        {
            long? measureId = null;
            if (line.LineKind == ReportLineKinds.Formula)
            {
                var measureKey = from.CalculatedMeasures.Single(measure => measure.CalculatedMeasureId == line.CalculatedMeasureId).MeasureKey;
                measureId = to.IdOf(ReportValueKinds.Calculated, measureKey);
                if (measureId is null || !remap.Contains(line.ValueColumnId!.Value))
                {
                    dropped.Add(line.ReportLineId);
                    continue;
                }
            }

            kept.Add((line, measureId));
        }

        // A kept line's parent is its nearest kept ancestor.
        ReportLineRecord[] clonedLines =
        [
            .. kept.Select(pair =>
            {
                var parent = pair.Source.ParentReportLineId;
                while (parent is { } parentId && dropped.Contains(parentId)) parent = sourceLines[parentId].ParentReportLineId;
                return pair.Source with
                {
                    ReportLineId = remap.Map(pair.Source.ReportLineId),
                    ReportVersionId = target.ReportVersionId,
                    ParentReportLineId = parent is { } keptParent ? remap.Map(keptParent) : null,
                    CalculatedMeasureId = pair.MeasureId,
                    ValueColumnId = pair.Source.ValueColumnId is { } columnId ? remap.Map(columnId) : null
                };
            })
        ];

        await columns.InsertManyAsync(cloned, cancellationToken);
        await lines.InsertManyAsync(clonedLines, cancellationToken);
        return new CreatedReportVersion
        {
            Version = target,
            DroppedColumns = droppedColumns,
            DroppedLines = [.. dropped.Select(id => sourceLines[id].LineKey).Order(StringComparer.Ordinal)]
        };
    }
}

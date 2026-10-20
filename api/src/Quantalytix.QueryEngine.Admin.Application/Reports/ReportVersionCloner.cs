using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Copies a report version's columns and lines into a new draft. They name metrics and members by key, so they copy as they
/// are; only the internal IDs are new, with each line's parent and value column remapped to the copies.
/// </summary>
public sealed class ReportVersionCloner(ITableGateway<ReportColumnRecord> columns, ITableGateway<ReportLineRecord> lines, IIdGenerator ids)
{
    /// <summary>Copies the source's columns and lines into the target.</summary>
    /// <param name="sourceVersionId">The report version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and inserts.</param>
    /// <returns>A task that completes when the copies are written.</returns>
    public async Task CloneAsync(long sourceVersionId, ReportVersionRecord target, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var source = new { ReportVersionId = sourceVersionId };
        var sourceColumns = await columns.ListAsync(source, cancellationToken);
        var sourceLines = await lines.ListAsync(source, cancellationToken);
        await columns.InsertManyAsync(
            [.. sourceColumns.Select(column => column with { ReportColumnId = remap.Map(column.ReportColumnId), ReportVersionId = target.ReportVersionId })],
            cancellationToken);
        await lines.InsertManyAsync(
            [
                .. sourceLines.Select(line => line with
                {
                    ReportLineId = remap.Map(line.ReportLineId),
                    ReportVersionId = target.ReportVersionId,
                    ParentReportLineId = remap.MapOptional(line.ParentReportLineId),
                    ValueColumnId = remap.MapOptional(line.ValueColumnId)
                })
            ],
            cancellationToken);
    }
}

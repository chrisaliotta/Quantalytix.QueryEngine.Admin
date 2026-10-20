using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Report version content stored in one table: rows owned by <c>report_version_id</c>.</summary>
/// <typeparam name="TRecord">The content row.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
/// <param name="rows">The content table.</param>
/// <param name="ids">Allocates the IDs of new rows.</param>
public abstract class ReportVersionRows<TRecord, TInput>(ITableGateway<TRecord> rows, IIdGenerator ids) : VersionRows<TRecord, TInput>(rows, ids)
    where TRecord : class
{
    /// <summary>The gateway filter of a report version's rows.</summary>
    /// <param name="versionId">The report version.</param>
    /// <returns>The filter object.</returns>
    protected sealed override object Owner(long versionId)
    {
        return new { ReportVersionId = versionId };
    }
}

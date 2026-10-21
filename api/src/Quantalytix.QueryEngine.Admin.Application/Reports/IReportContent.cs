using Quantalytix.QueryEngine.Admin.Application.Content;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Report content edited row by row: the source profiles and calculation dimensions of a report. Each kind knows what a write
/// does to the report's graph, so <see cref="ReportChanges"/> can check a write to an enabled report before it is made.
/// </summary>
/// <typeparam name="TItem">The content row as the API returns it.</typeparam>
/// <typeparam name="TInput">The row as the create and edit endpoints take it.</typeparam>
public interface IReportContent<TItem, TInput> : IOwnedContent<TItem, TInput>
{
    /// <summary>The report with a row added, or in place of the stored row with its ID.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="item">A row <see cref="IOwnedContent{TItem, TInput}.Compose"/> made.</param>
    /// <returns>The report as the write leaves it.</returns>
    ReportGraph Put(ReportGraph report, TItem item);

    /// <summary>The report without a row and the rows that only it gives meaning to.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="itemId">The row, one of the report's.</param>
    /// <returns>The report as the delete leaves it.</returns>
    ReportGraph Without(ReportGraph report, long itemId);
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Points <c>qe_report.current_report_version_id</c> at a report version when it is published. The pointer is authoring-only:
/// releases pin report versions themselves, so it changes nothing queries read.
/// </summary>
public sealed class CurrentReportVersionUpdater(ITableGateway<ReportVersionRecord> versions, ITableGateway<ReportRecord> reports)
    : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the updater joins: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Validation derives nothing here.</summary>
    /// <param name="versionId">The report version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Makes the newly published version its report's current version.</summary>
    /// <param name="versionId">The newly published report version.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>A task that completes when the pointer is written.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        var report = await reports.GetAsync(new { version.ReportId }, cancellationToken);
        await reports.UpdateAsync(report with { CurrentReportVersionId = versionId }, cancellationToken);
    }
}

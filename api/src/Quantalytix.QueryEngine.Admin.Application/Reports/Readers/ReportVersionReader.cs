using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads reports with their versions: one report's live or stored version, and every report a snapshot holds.</summary>
/// <param name="reports">The report identities.</param>
/// <param name="versions">The report versions.</param>
/// <param name="live">The live snapshot.</param>
public sealed class ReportVersionReader(ITableGateway<ReportRecord> reports, ITableGateway<ReportVersionRecord> versions, LiveSnapshot live)
{
    /// <summary>Reads a report with its live version.</summary>
    /// <param name="reportId">The report, which the live snapshot holds.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report.</returns>
    public async Task<VersionedReport> LiveAsync(long reportId, CancellationToken cancellationToken)
    {
        var version = (await versions.ListAsync(new { ReportId = reportId, SnapshotId = await live.IdAsync(cancellationToken) }, cancellationToken)).Single();
        return new VersionedReport(await reports.GetAsync(new { ReportId = reportId }, cancellationToken), version);
    }

    /// <summary>Reads the report of a stored version.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report.</returns>
    public async Task<VersionedReport> VersionAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = reportVersionId }, cancellationToken);
        return new VersionedReport(await reports.GetAsync(new { version.ReportId }, cancellationToken), version);
    }

    /// <summary>Reads every report a snapshot holds a version of, or those of one calculation model.</summary>
    /// <param name="snapshotId">The snapshot.</param>
    /// <param name="calculationModelId">The model whose readers to list; null for every report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The reports by key.</returns>
    public async Task<IReadOnlyList<VersionedReport>> InSnapshotAsync(long snapshotId, long? calculationModelId, CancellationToken cancellationToken)
    {
        var held = await versions.ListAsync(calculationModelId is { } modelId ? (object)new { SnapshotId = snapshotId, CalculationModelId = modelId } : new { SnapshotId = snapshotId }, cancellationToken);
        var byId = (await reports.ListAsync(new { ReportId = held.Select(version => version.ReportId).ToArray() }, cancellationToken)).ToDictionary(report => report.ReportId);
        return [.. held.Select(version => new VersionedReport(byId[version.ReportId], version)).OrderBy(report => report.ReportKey, StringComparer.Ordinal)];
    }
}

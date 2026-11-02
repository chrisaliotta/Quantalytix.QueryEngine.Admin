using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads a report version as its change set, its document and its checks start from: the live one, or any stored one.</summary>
/// <param name="reports">Reads the report with its version.</param>
/// <param name="reader">Reads the report's content and its model's metrics.</param>
/// <param name="owned">Reads the report dimension's version.</param>
/// <param name="roles">The role table.</param>
/// <param name="datasetVersions">The dataset versions, which tell the datasets the snapshot holds.</param>
/// <param name="live">The live snapshot.</param>
public sealed class ReportSnapshotLoader(
    ReportVersionReader reports,
    ReportGraphReader reader,
    OwnedVersions owned,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    LiveSnapshot live)
{
    /// <summary>Reads a report's live version with what its operations consult around it.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ReportSnapshot> LoadAsync(long reportId, CancellationToken cancellationToken)
    {
        return await LoadAsync(await reports.LiveAsync(reportId, cancellationToken), cancellationToken);
    }

    /// <summary>Reads a stored report version with what surrounds it in its snapshot.</summary>
    /// <param name="reportVersionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ReportSnapshot> LoadVersionAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        return await LoadAsync(await reports.VersionAsync(reportVersionId, cancellationToken), cancellationToken);
    }

    /// <summary>Reads a report version with its surroundings.</summary>
    private async Task<ReportSnapshot> LoadAsync(VersionedReport report, CancellationToken cancellationToken)
    {
        var snapshotId = report.Version.SnapshotId;
        return new ReportSnapshot
        {
            Content = await reader.ReadAsync(report, cancellationToken),
            Surroundings = new ReportSurroundings
            {
                LiveSnapshotId = await live.IdAsync(cancellationToken),
                DimensionVersionId = (await owned.StructureAsync(report.Report, snapshotId, cancellationToken)).DimensionVersionId,
                Roles = await roles.ListAsync(null, cancellationToken),
                DatasetIds = (await datasetVersions.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).Select(version => version.DatasetId).ToHashSet()
            }
        };
    }
}

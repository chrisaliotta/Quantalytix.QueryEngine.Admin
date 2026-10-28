using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads a report as its change set, its document and its checks start from.</summary>
/// <param name="reports">The report table.</param>
/// <param name="reader">Reads the report's content and its model's metrics.</param>
/// <param name="owned">Reads the report dimension's version.</param>
/// <param name="roles">The role table.</param>
/// <param name="datasets">The dataset table.</param>
public sealed class ReportSnapshotLoader(
    ITableGateway<ReportRecord> reports,
    ReportGraphReader reader,
    OwnedVersions owned,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DatasetRecord> datasets)
{
    /// <summary>Reads a report with what its operations consult around it.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<ReportSnapshot> LoadAsync(long reportId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        return new ReportSnapshot
        {
            Content = await reader.ReadAsync(report, cancellationToken),
            Surroundings = new ReportSurroundings
            {
                DimensionVersionId = (await owned.StructureAsync(report, cancellationToken)).DimensionVersionId,
                Roles = await roles.ListAsync(null, cancellationToken),
                DatasetIds = (await datasets.ListAsync(null, cancellationToken)).Select(dataset => dataset.DatasetId).ToHashSet()
            }
        };
    }
}

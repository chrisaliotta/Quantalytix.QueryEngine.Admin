using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Reads the report sources that own report bridges (B9) in one snapshot: <c>qe_report_source.bridge_id</c> of the snapshot's report
/// versions, with the report's key.
/// </summary>
public sealed class BridgeOwnerReader(
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<ReportRecord> reports)
{
    /// <summary>Reads the owners of bridges in a snapshot.</summary>
    /// <param name="bridgeIds">The bridges.</param>
    /// <param name="snapshotId">The snapshot whose report sources own them.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The owner of each report bridge among them, by bridge id; analytical bridges have none.</returns>
    public async Task<IReadOnlyDictionary<long, BridgeOwner>> OwnersAsync(IEnumerable<long> bridgeIds, long snapshotId, CancellationToken cancellationToken)
    {
        var reportOf = (await reportVersions.ListAsync(new { SnapshotId = snapshotId }, cancellationToken)).ToDictionary(version => version.ReportVersionId, version => version.ReportId);
        var owning = await sources.ListAsync(new { BridgeId = bridgeIds.ToArray(), ReportVersionId = reportOf.Keys.ToArray() }, cancellationToken);
        var reportKeys = (await reports.ListAsync(new { ReportId = reportOf.Values.Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        return owning.ToDictionary(
            source => source.BridgeId!.Value,
            source => new BridgeOwner { ReportId = reportOf[source.ReportVersionId], ReportKey = reportKeys[reportOf[source.ReportVersionId]], SourceKey = source.SourceKey });
    }
}

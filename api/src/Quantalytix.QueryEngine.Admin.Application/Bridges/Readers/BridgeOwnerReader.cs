using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Reads the report sources that own report bridges (B9): <c>qe_report_source.bridge_id</c> with its report's key.</summary>
public sealed class BridgeOwnerReader(ITableGateway<ReportSourceRecord> sources, ITableGateway<ReportRecord> reports)
{
    /// <summary>Reads the owners of bridges.</summary>
    /// <param name="bridgeIds">The bridges.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The owner of each report bridge among them, by bridge id; analytical bridges have none.</returns>
    public async Task<IReadOnlyDictionary<long, BridgeOwner>> OwnersAsync(IEnumerable<long> bridgeIds, CancellationToken cancellationToken)
    {
        var owning = await sources.ListAsync(new { BridgeId = bridgeIds.ToArray() }, cancellationToken);
        var reportKeys = (await reports.ListAsync(new { ReportId = owning.Select(source => source.ReportId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        return owning.ToDictionary(
            source => source.BridgeId!.Value,
            source => new BridgeOwner { ReportId = source.ReportId, ReportKey = reportKeys[source.ReportId], SourceKey = source.SourceKey });
    }
}

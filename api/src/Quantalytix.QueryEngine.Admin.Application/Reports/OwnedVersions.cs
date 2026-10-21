using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Reads the one version of each object a report owns, which its editor edits in place: the report dimension's, whose members are
/// the report's member rows, and each bridge source's bridge's, which holds the source's entries.
/// </summary>
public sealed class OwnedVersions(ITableGateway<DimensionVersionRecord> dimensionVersions, ITableGateway<BridgeVersionRecord> bridgeVersions)
{
    /// <summary>The report dimension's version.</summary>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version whose members are the member rows.</returns>
    public async Task<DimensionVersionRecord> StructureAsync(ReportRecord report, CancellationToken cancellationToken)
    {
        return (await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId }, cancellationToken)).Single();
    }

    /// <summary>The version of each bridge source's bridge.</summary>
    /// <param name="sources">Sources of a report; direct sources have none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bridge versions, by source ID.</returns>
    public async Task<IReadOnlyDictionary<long, BridgeVersionRecord>> BridgesAsync(IEnumerable<ReportSourceRecord> sources, CancellationToken cancellationToken)
    {
        var sourceOf = sources.Where(source => source.BridgeId is not null).ToDictionary(source => source.BridgeId!.Value, source => source.ReportSourceId);
        return (await bridgeVersions.ListAsync(new { BridgeId = sourceOf.Keys.ToArray() }, cancellationToken)).ToDictionary(version => sourceOf[version.BridgeId]);
    }
}

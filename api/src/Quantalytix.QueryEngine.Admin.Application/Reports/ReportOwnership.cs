using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Finds the report that owns a dimension or a bridge. A report owns its report dimension and every bridge into it, one per bridge
/// source; only the report's editor writes them, and historical assumption sets do not capture them, because reports always run on
/// the active set.
/// </summary>
public sealed class ReportOwnership(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<ReportRecord> reports)
{
    /// <summary>The IDs of every report dimension.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The dimension IDs.</returns>
    public async Task<IReadOnlySet<long>> ReportDimensionIdsAsync(CancellationToken cancellationToken)
    {
        return (await dimensions.ListAsync(new { DimensionPurpose = DimensionPurposes.Report }, cancellationToken))
            .Select(dimension => dimension.DimensionId)
            .ToHashSet();
    }

    /// <summary>The IDs of every report dimension and of every bridge into one.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report-owned dimension and bridge IDs.</returns>
    public async Task<(IReadOnlySet<long> Dimensions, IReadOnlySet<long> Bridges)> OwnedIdsAsync(CancellationToken cancellationToken)
    {
        var reportDimensions = await ReportDimensionIdsAsync(cancellationToken);
        var reportBridges = (await bridges.ListAsync(new { TargetDimensionId = reportDimensions.ToArray() }, cancellationToken))
            .Select(bridge => bridge.BridgeId)
            .ToHashSet();
        return (reportDimensions, reportBridges);
    }

    /// <summary>The report whose rows a dimension holds.</summary>
    /// <param name="dimensionId">A dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The owning report, or null for an analytical dimension.</returns>
    public async Task<ReportRecord?> OfDimensionAsync(long dimensionId, CancellationToken cancellationToken)
    {
        return (await reports.ListAsync(new { ReportDimensionId = dimensionId }, cancellationToken)).SingleOrDefault();
    }

    /// <summary>The report whose rows a dimension version holds.</summary>
    /// <param name="dimensionVersionId">A dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The owning report, or null for a version of an analytical dimension.</returns>
    public async Task<ReportRecord?> OfDimensionVersionAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var version = await dimensionVersions.GetAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return await OfDimensionAsync(version.DimensionId, cancellationToken);
    }

    /// <summary>The report whose dimension a bridge leads into.</summary>
    /// <param name="bridgeId">A bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The owning report, or null for an analytical bridge.</returns>
    public async Task<ReportRecord?> OfBridgeAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var bridge = await bridges.GetAsync(new { BridgeId = bridgeId }, cancellationToken);
        return await OfDimensionAsync(bridge.TargetDimensionId, cancellationToken);
    }

    /// <summary>The report whose dimension a bridge version leads into.</summary>
    /// <param name="bridgeVersionId">A bridge version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The owning report, or null for a version of an analytical bridge.</returns>
    public async Task<ReportRecord?> OfBridgeVersionAsync(long bridgeVersionId, CancellationToken cancellationToken)
    {
        var version = await bridgeVersions.GetAsync(new { BridgeVersionId = bridgeVersionId }, cancellationToken);
        return await OfBridgeAsync(version.BridgeId, cancellationToken);
    }
}

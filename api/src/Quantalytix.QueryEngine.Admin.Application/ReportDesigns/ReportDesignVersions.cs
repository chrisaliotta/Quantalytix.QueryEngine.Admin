using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Pairs a report version with the versions of the objects its report owns that have the same status: a draft with the drafts of
/// the report dimension and its bridges, the published version with their published versions. A report has at most one draft, and
/// its owned objects are drafted, validated and published with it, so the pairing is unambiguous for draft, validated and
/// published report versions.
/// </summary>
public sealed class ReportDesignVersions(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> reportVersions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions)
{
    /// <summary>Reads a report version's design scope.</summary>
    /// <param name="reportVersionId">A draft, validated or published report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report version with its report dimension's version and its bridges' versions of the same status.</returns>
    public async Task<ReportDesignScope> ReadAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var version = await reportVersions.GetAsync(new { ReportVersionId = reportVersionId }, cancellationToken);
        var report = await reports.GetAsync(new { version.ReportId }, cancellationToken);
        var structure = (await dimensionVersions.ListAsync(new { DimensionId = report.ReportDimensionId, version.Status }, cancellationToken)).Single();
        var owned = (await bridges.ListAsync(new { TargetDimensionId = report.ReportDimensionId }, cancellationToken)).ToDictionary(bridge => bridge.BridgeId);
        var paired = await bridgeVersions.ListAsync(new { BridgeId = owned.Keys.ToArray(), version.Status }, cancellationToken);
        return new ReportDesignScope
        {
            Report = report,
            Version = version,
            Structure = structure,
            Bridges = owned,
            BridgeVersions = paired.ToDictionary(bridgeVersion => bridgeVersion.BridgeId)
        };
    }

    /// <summary>
    /// The versions a design's status change moves together, in the order their statuses are written: the report dimension's
    /// version, then each bridge's version, then the report version, so each is published after the versions it names keys of.
    /// </summary>
    /// <param name="reportVersionId">A draft, validated or published report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The versions of the design.</returns>
    public async Task<IReadOnlyList<VersionReference>> SetAsync(long reportVersionId, CancellationToken cancellationToken)
    {
        var scope = await ReadAsync(reportVersionId, cancellationToken);
        return
        [
            new VersionReference { Kind = VersionKinds.DimensionVersion, Id = scope.Structure.DimensionVersionId },
            .. scope.BridgeVersions.Values
                .OrderBy(bridgeVersion => bridgeVersion.BridgeId)
                .Select(bridgeVersion => new VersionReference { Kind = VersionKinds.BridgeVersion, Id = bridgeVersion.BridgeVersionId }),
            new VersionReference { Kind = VersionKinds.ReportVersion, Id = scope.Version.ReportVersionId }
        ];
    }
}

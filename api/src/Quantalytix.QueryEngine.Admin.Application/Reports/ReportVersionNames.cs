using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Names report versions like <c>'bank.margin_statement' v2</c>, as delete blockers and live-use checks list them.</summary>
public sealed class ReportVersionNames(ITableGateway<ReportRecord> reports, ITableGateway<ReportVersionRecord> versions)
{
    /// <summary>Names report versions by ID.</summary>
    /// <param name="versionIds">Report version IDs.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The name of each version, by ID.</returns>
    public async Task<IReadOnlyDictionary<long, string>> OfIdsAsync(IEnumerable<long> versionIds, CancellationToken cancellationToken)
    {
        return await OfAsync(await versions.ListAsync(new { ReportVersionId = versionIds.Distinct().ToArray() }, cancellationToken), cancellationToken);
    }

    /// <summary>Names report versions.</summary>
    /// <param name="named">Report versions.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The name of each version, by ID.</returns>
    public async Task<IReadOnlyDictionary<long, string>> OfAsync(IReadOnlyList<ReportVersionRecord> named, CancellationToken cancellationToken)
    {
        var keys = (await reports.ListAsync(new { ReportId = named.Select(version => version.ReportId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(report => report.ReportId, report => report.ReportKey);
        return named.ToDictionary(version => version.ReportVersionId, version => $"'{keys[version.ReportId]}' v{version.VersionNumber}");
    }
}

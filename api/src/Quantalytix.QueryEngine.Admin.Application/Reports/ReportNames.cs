using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Names reports like <c>report 'bank.margin_statement'</c>, as delete blockers and live-use checks list them.</summary>
public sealed class ReportNames(ITableGateway<ReportRecord> reports)
{
    /// <summary>Names reports by ID.</summary>
    /// <param name="reportIds">Report IDs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The name of each report, by ID.</returns>
    public async Task<IReadOnlyDictionary<long, string>> OfIdsAsync(IEnumerable<long> reportIds, CancellationToken cancellationToken)
    {
        return Of(await reports.ListAsync(new { ReportId = reportIds.Distinct().ToArray() }, cancellationToken));
    }

    /// <summary>Names reports.</summary>
    /// <param name="named">Reports.</param>
    /// <returns>The name of each report, by ID.</returns>
    public static IReadOnlyDictionary<long, string> Of(IEnumerable<ReportRecord> named)
    {
        return named.ToDictionary(report => report.ReportId, report => $"report '{report.ReportKey}'");
    }
}

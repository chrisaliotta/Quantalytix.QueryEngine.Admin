using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Enables and disables reports. Only enabled reports reach the engine, so enabling a report checks it against every report rule
/// and refuses it on any violation; disabling always succeeds.
/// </summary>
/// <param name="reports">Reads and writes the report.</param>
/// <param name="reader">Reads the report's content.</param>
/// <param name="rules">Checks the report.</param>
public sealed class ReportStatusService(ITableGateway<ReportRecord> reports, ReportGraphReader reader, ReportRules rules)
{
    /// <summary>Enables or disables a report.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">Whether the report is enabled.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The violations that keep the report from being enabled, in which case it stays disabled; none when it was set.</returns>
    public async Task<IReadOnlyList<RuleViolation>> SetAsync(long reportId, ReportStatusInput input, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        if (input.IsEnabled && await CheckAsync(report, cancellationToken) is { Count: > 0 } violations) return violations;
        await reports.UpdateAsync(report with { IsEnabled = input.IsEnabled }, cancellationToken);
        return [];
    }

    /// <summary>Checks a report against every report rule, enabled or not, and changes nothing.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found; none when the report could be enabled.</returns>
    public async Task<IReadOnlyList<RuleViolation>> CheckAsync(long reportId, CancellationToken cancellationToken)
    {
        return await CheckAsync(await reports.GetAsync(new { ReportId = reportId }, cancellationToken), cancellationToken);
    }

    /// <summary>Checks a report as it stands.</summary>
    private async Task<IReadOnlyList<RuleViolation>> CheckAsync(ReportRecord report, CancellationToken cancellationToken)
    {
        return await rules.EvaluateAsync([await reader.ReadAsync(report, cancellationToken)], cancellationToken);
    }
}

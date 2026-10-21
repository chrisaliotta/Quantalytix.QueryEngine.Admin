using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Applies a write to a report's content. While the report is enabled, the engine reads every write at once, so the write is
/// first applied to the report in memory and checked against every report rule; one that breaks a rule writes nothing. A disabled
/// report takes any write, and enabling it checks the result.
/// </summary>
/// <param name="reports">Reads whether the report is enabled.</param>
/// <param name="reader">Reads the report as it stands.</param>
/// <param name="rules">Checks the report as the write would leave it.</param>
public sealed class ReportChanges(ITableGateway<ReportRecord> reports, ReportGraphReader reader, ReportRules rules)
{
    /// <summary>Checks a write against the report rules when the report is enabled, and makes it when it passes.</summary>
    /// <param name="reportId">The report written to.</param>
    /// <param name="change">The write's effect on the report's content.</param>
    /// <param name="write">Makes the write.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The violations the write would cause, in which case nothing was written; none when it was made.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ApplyAsync(
        long reportId,
        Func<ReportGraph, ReportGraph> change,
        Func<Task> write,
        CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        if (report.IsEnabled)
        {
            var violations = await rules.EvaluateAsync([change(await reader.ReadAsync(report, cancellationToken))], cancellationToken);
            if (violations.Count > 0) return violations;
        }

        await write();
        return [];
    }
}

using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Runs every report rule on reports, in the scope of the active release.</summary>
/// <param name="rules">The report rules.</param>
/// <param name="reader">Reads the scope.</param>
public sealed class ReportRules(IEnumerable<IReportRule> rules, ReportGraphReader reader)
{
    /// <summary>Checks reports against every report rule.</summary>
    /// <param name="reports">The reports, as they stand or as a write would leave them.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found; none when every report passes.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IReadOnlyList<ReportGraph> reports, CancellationToken cancellationToken)
    {
        var scope = await reader.ScopeAsync(reports, null, cancellationToken);
        return [.. reports.SelectMany(report => rules.SelectMany(rule => rule.Evaluate(report, scope)))];
    }
}

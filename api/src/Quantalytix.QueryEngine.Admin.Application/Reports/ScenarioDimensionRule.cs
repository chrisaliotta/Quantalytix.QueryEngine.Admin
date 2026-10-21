using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>scenario_dimension</c>: a report has at most one enabled scenario dimension, because a run or comparison names one scenario,
/// a member of that dimension, whenever one of its sources reaches it.
/// </summary>
public sealed class ScenarioDimensionRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "scenario_dimension";

    /// <summary>Reports a report with more than one enabled scenario dimension.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: calculation dimensions are the report's own.</param>
    /// <returns>One violation when the report has several scenario dimensions.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        string[] scenarios = [.. report.EnabledDimensions.Where(dimension => dimension.IsScenario).Select(dimension => dimension.DimensionKey).Order(StringComparer.Ordinal)];
        if (scenarios.Length > 1)
        {
            yield return new RuleViolation
            {
                Rule = Code,
                Message = $"Report '{report.ReportKey}' has scenario dimensions {string.Join(", ", scenarios.Select(key => $"'{key}'"))}; a report has at most one."
            };
        }
    }
}

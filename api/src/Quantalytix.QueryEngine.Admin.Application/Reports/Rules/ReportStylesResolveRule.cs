using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_styles_resolve</c>: every style a report's rows name is one of the report's styles, and each row role has at most one
/// default style, because a row that names no style shows its role's default, which must be one style.
/// </summary>
public sealed class ReportStylesResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_styles_resolve";

    /// <summary>Reports each row that names a style the report lacks, and each role with more than one default style.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: styles and rows are the report's own.</param>
    /// <returns>One violation per problem.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        foreach (var violation in Unresolved(report)) yield return violation;

        var defaults = report.Styles
            .Where(style => style.DefaultRole is not null)
            .GroupBy(style => style.DefaultRole!, StringComparer.Ordinal)
            .Where(role => role.Count() > 1)
            .OrderBy(role => role.Key, StringComparer.Ordinal);
        foreach (var role in defaults)
        {
            var keys = string.Join(", ", role.Select(style => $"'{style.StyleKey}'").Order(StringComparer.Ordinal));
            yield return Violation($"Report '{report.ReportKey}' styles {keys} are each the default of {role.Key} rows; a role has at most one default style.", ReportAreas.Target(ReportAreas.Styles, role.First().StyleKey));
        }
    }

    /// <summary>Reports each row that names a style the report lacks, which no report may keep (a gate even while disabled).</summary>
    /// <param name="report">The report.</param>
    /// <returns>One violation per such row.</returns>
    public static IEnumerable<RuleViolation> Unresolved(ReportGraph report)
    {
        var styleKeys = report.Styles.Select(style => style.StyleKey).ToHashSet(StringComparer.Ordinal);
        return report.MemberOptions.Select(option => (Row: option.MemberKey, option.StyleKey))
            .Concat(report.Lines.Select(line => (Row: line.LineKey, line.StyleKey)))
            .Where(row => row.StyleKey is not null && !styleKeys.Contains(row.StyleKey))
            .OrderBy(row => row.Row, StringComparer.Ordinal)
            .Select(row => Violation(
                $"Report '{report.ReportKey}' row '{row.Row}' shows style '{row.StyleKey}', which is no style of the report.",
                ReportAreas.Target(ReportAreas.Rows, row.Row)));
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message, ViolationTarget target)
    {
        return new RuleViolation { Code = Code, Message = message, Target = target };
    }
}

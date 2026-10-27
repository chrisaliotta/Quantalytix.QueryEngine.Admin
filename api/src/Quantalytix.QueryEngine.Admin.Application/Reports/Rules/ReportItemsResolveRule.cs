using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_items_resolve</c>: the report's rows are one tree, and every key that names a row resolves. The report dimension has
/// one root; member options, the parents of overlay rows that are no overlay row, and variable scopes name members of the report
/// dimension; and no overlay row has a member's key, because rows are keyed by both.
/// </summary>
public sealed class ReportItemsResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_items_resolve";

    /// <summary>Reports every row key that leaves the report dimension's members, and a tree without one root.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: the report dimension is the report's own.</param>
    /// <returns>The violations found.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var subject = $"Report '{report.ReportKey}'";
        var memberKeys = report.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        var roots = report.Members.Count(member => member.ParentDimensionMemberId is null);
        if (roots != 1) yield return Violation($"{subject} has {roots} top-level member rows; it needs one, the report's total.");
        var overlayKeys = report.Lines.Select(line => line.LineKey).ToHashSet(StringComparer.Ordinal);
        foreach (var option in report.MemberOptions.Where(option => !memberKeys.Contains(option.MemberKey)))
            yield return Violation($"{subject} has an option for row '{option.MemberKey}', which is no member row.");
        foreach (var line in report.Lines.Where(line => line.ParentKey is { } parent && !overlayKeys.Contains(parent) && !memberKeys.Contains(parent)))
            yield return Violation($"{subject} row '{line.LineKey}' sits under '{line.ParentKey}', which is no row of the report.");
        foreach (var line in report.Lines.Where(line => memberKeys.Contains(line.LineKey)))
            yield return Violation($"{subject} row '{line.LineKey}' has the key of a member row; rows are keyed by both.");
        foreach (var scoped in report.Scopes.Where(scoped => !memberKeys.Contains(scoped.ScopeMemberKey)))
            yield return Violation($"{subject} scopes variable '{scoped.VariableKey}' to row '{scoped.ScopeMemberKey}', which is no member row.");
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}

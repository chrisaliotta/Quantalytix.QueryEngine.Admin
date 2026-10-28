using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_items_resolve</c>: the report's rows are one tree, and every key that names a row or an item resolves. The report
/// dimension has one root; member options, the parents of overlay rows that are no overlay row, and variable scopes name members of
/// the report dimension; no overlay row has a member's key, because rows are keyed by both. Each bridge source's rules add into data
/// rows and take members of the published version of their role's dimension, an assignable one when a rule takes the member alone.
/// </summary>
public sealed class ReportItemsResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_items_resolve";

    /// <summary>Reports every row or item key that does not resolve, and a tree without one root.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the published versions of the sources' role dimensions.</param>
    /// <returns>The violations found.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var subject = $"Report '{report.ReportKey}'";
        var memberKeys = report.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        var dataKeys = report.Members.Where(member => member.IsAssignable).Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        var roots = report.Members.Count(member => member.ParentDimensionMemberId is null);
        if (roots != 1) yield return Violation($"{subject} has {roots} top-level member rows; it needs one, the report's total.", ReportAreas.Target(ReportAreas.Rows));
        var overlayKeys = report.Lines.Select(line => line.LineKey).ToHashSet(StringComparer.Ordinal);
        foreach (var option in report.MemberOptions.Where(option => !memberKeys.Contains(option.MemberKey)))
            yield return Violation($"{subject} has an option for row '{option.MemberKey}', which is no member row.", Row(option.MemberKey));
        foreach (var line in report.Lines.Where(line => line.ParentKey is { } parent && !overlayKeys.Contains(parent) && !memberKeys.Contains(parent)))
            yield return Violation($"{subject} row '{line.LineKey}' sits under '{line.ParentKey}', which is no row of the report.", Row(line.LineKey));
        foreach (var line in report.Lines.Where(line => memberKeys.Contains(line.LineKey)))
            yield return Violation($"{subject} row '{line.LineKey}' has the key of a member row; rows are keyed by both.", Row(line.LineKey));
        foreach (var scoped in report.Scopes.Where(scoped => !memberKeys.Contains(scoped.ScopeMemberKey)))
            yield return Violation(
                $"{subject} scopes variable '{scoped.VariableKey}' to row '{scoped.ScopeMemberKey}', which is no member row.",
                ReportAreas.Target(ReportAreas.Scopes, scoped.VariableKey));

        var rulesOf = report.SourceItems.ToLookup(item => item.ReportSourceId);
        foreach (var source in report.Sources.Where(source => source.DimensionRoleId is not null).OrderBy(source => source.SourceKey, StringComparer.Ordinal))
        {
            var target = ReportAreas.Target(ReportAreas.Sources, source.SourceKey);
            var items = scope.Templates[source.DimensionRoleId!.Value];
            foreach (var rule in rulesOf[source.ReportSourceId].OrderBy(rule => rule.RowKey, StringComparer.Ordinal).ThenBy(rule => rule.ItemKey, StringComparer.Ordinal))
            {
                var at = $"{subject} source '{source.SourceKey}' maps '{rule.ItemKey}' into '{rule.RowKey}'";
                if (!dataKeys.Contains(rule.RowKey)) yield return Violation($"{at}, which is no data row.", target);
                if (items is null || !items.MemberKeys.Contains(rule.ItemKey))
                    yield return Violation($"{at}, which is no member of the published version of its role's dimension.", target);
                else if (rule.Selection == SourceSelections.Member && items.UnassignableMemberKeys.Contains(rule.ItemKey))
                    yield return Violation($"{at}, which takes no values of its own; take every item under it instead.", target);
            }
        }
    }

    /// <summary>The target of a row.</summary>
    private static ViolationTarget Row(string rowKey)
    {
        return ReportAreas.Target(ReportAreas.Rows, rowKey);
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message, ViolationTarget target)
    {
        return new RuleViolation { Code = Code, Message = message, Target = target };
    }
}

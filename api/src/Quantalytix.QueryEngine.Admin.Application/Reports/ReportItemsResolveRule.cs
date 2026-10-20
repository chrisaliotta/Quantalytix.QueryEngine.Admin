using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_items_resolve</c>: a report's dimension has a published version with one root that contains every member line's key
/// and every target member of the model version's variables. Sources classify into the version the active assumption set pins,
/// the orchestrator aligns them on its members, and it evaluates formula lines at the root. Report versions and calculation
/// model versions both run the rule; publishing a dimension version checks the same keys through
/// <c>dimension_consumers_resolve</c>.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports with the model version they are checked against.</param>
/// <param name="dimensions">Reads the report dimension's published version.</param>
public sealed class ReportItemsResolveRule(string kind, ReportGraphReader reader, PublishedDimensionReader dimensions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_items_resolve";

    /// <summary>The kind the rule checks: report versions or calculation model versions.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports every report whose names leave the published report-item tree.</summary>
    /// <param name="versionId">The report or model version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            if (await dimensions.ReadAsync(report.Version.ReportDimensionId, cancellationToken) is not { } items)
            {
                violations.Add(Violation($"Report '{report.ReportKey}' reads a report dimension with no published version."));
                continue;
            }

            if (items.RootCount != 1)
                violations.Add(Violation($"Report '{report.ReportKey}' reads a report dimension whose published version has {items.RootCount} roots; it needs one."));
            foreach (var line in report.Lines.Where(line => line.ReportMemberKey is { } key && !items.MemberKeys.Contains(key)))
                violations.Add(Violation($"Report '{report.ReportKey}' line '{line.LineKey}' shows member '{line.ReportMemberKey}', which the published report-item version lacks."));
            foreach (var variable in report.Variables.Where(variable => variable.TargetMemberKey is { } key && !items.MemberKeys.Contains(key)))
                violations.Add(Violation($"Report '{report.ReportKey}' variable '{variable.VariableKey}' targets member '{variable.TargetMemberKey}', which the published report-item version lacks."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

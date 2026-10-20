using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_items_resolve</c>: a report's dimension has a published version with one root, whose members are the report's rows. Every
/// member option and variable target names one of its members, every overlay row sits under one of its members or another overlay
/// row, and no overlay row's key is also a member key, because rows are keyed by both. Sources classify into the version the active
/// assumption set pins and the orchestrator evaluates formula rows at the root. Publishing a dimension version checks the same keys
/// through <c>dimension_consumers_resolve</c>.
/// </summary>
/// <param name="reader">Reads the report version.</param>
/// <param name="dimensions">Reads the report dimension's published version.</param>
public sealed class ReportItemsResolveRule(ReportGraphReader reader, PublishedDimensionReader dimensions) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_items_resolve";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports every key of the report version that leaves its dimension's published members.</summary>
    /// <param name="versionId">The report version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            var subject = $"Report '{report.ReportKey}'";
            if (await dimensions.ReadAsync(report.Report.ReportDimensionId, cancellationToken) is not { } items)
            {
                violations.Add(Violation($"{subject} reads a report dimension with no published version."));
                continue;
            }

            const string lacking = "which the published version of the report dimension lacks";
            if (items.RootCount != 1)
                violations.Add(Violation($"{subject} reads a report dimension whose published version has {items.RootCount} roots; it needs one."));
            var overlayKeys = report.Lines.Select(line => line.LineKey).ToHashSet(StringComparer.Ordinal);
            foreach (var option in report.MemberOptions.Where(option => !items.MemberKeys.Contains(option.MemberKey)))
                violations.Add(Violation($"{subject} has an option for member '{option.MemberKey}', {lacking}."));
            foreach (var line in report.Lines.Where(line => line.ParentKey is { } parent && !overlayKeys.Contains(parent) && !items.MemberKeys.Contains(parent)))
                violations.Add(Violation($"{subject} row '{line.LineKey}' sits under '{line.ParentKey}', which is no overlay row and no member the published version of the report dimension has."));
            foreach (var line in report.Lines.Where(line => items.MemberKeys.Contains(line.LineKey)))
                violations.Add(Violation($"{subject} row '{line.LineKey}' has the key of a member of the report dimension; rows are keyed by both."));
            foreach (var target in report.Targets.Where(target => !items.MemberKeys.Contains(target.TargetMemberKey)))
                violations.Add(Violation($"{subject} targets variable '{target.VariableKey}' at member '{target.TargetMemberKey}', {lacking}."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

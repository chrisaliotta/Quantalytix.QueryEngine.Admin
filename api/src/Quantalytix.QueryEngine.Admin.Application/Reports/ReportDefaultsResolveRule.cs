using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_defaults_resolve</c>: the default filter compares enabled, filterable calculation dimensions of the model version,
/// and the default pivot breaks out by enabled, groupable ones, because every run of the report applies them. Report versions
/// and calculation model versions both run the rule.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports with the model version they are checked against.</param>
public sealed class ReportDefaultsResolveRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_defaults_resolve";

    /// <summary>The kind the rule checks: report versions or calculation model versions.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports each default dimension that the model version cannot serve.</summary>
    /// <param name="versionId">The report or model version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unserved dimension.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            var available = report.Dimensions.ToDictionary(dimension => dimension.DimensionKey, StringComparer.Ordinal);
            violations.AddRange(ReportDefaults.FilterDimensions(report.Version.DefaultFilterJson)
                .Where(key => available.GetValueOrDefault(key) is not { IsFilterable: true })
                .Select(key => Violation($"Report '{report.ReportKey}' filters by default on '{key}', which is no filterable calculation dimension of the model version.")));
            violations.AddRange(ReportDefaults.PivotDimensions(report.Version.DefaultPivotJson)
                .Where(key => available.GetValueOrDefault(key) is not { IsGroupable: true })
                .Select(key => Violation($"Report '{report.ReportKey}' breaks out by default by '{key}', which is no groupable calculation dimension of the model version.")));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

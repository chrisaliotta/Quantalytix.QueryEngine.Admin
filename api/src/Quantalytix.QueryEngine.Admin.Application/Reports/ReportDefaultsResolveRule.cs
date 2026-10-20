using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_defaults_resolve</c>: the default filter compares enabled, filterable calculation dimensions of the report version, and
/// the default pivot breaks out by enabled, groupable ones, because every run of the report applies them.
/// </summary>
/// <param name="reader">Reads the report version.</param>
public sealed class ReportDefaultsResolveRule(ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_defaults_resolve";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports each default dimension that the report version cannot serve.</summary>
    /// <param name="versionId">The report version being validated.</param>
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
                .Select(key => Violation($"Report '{report.ReportKey}' filters by default on '{key}', which is no filterable calculation dimension of the report version.")));
            violations.AddRange(ReportDefaults.PivotDimensions(report.Version.DefaultPivotJson)
                .Where(key => available.GetValueOrDefault(key) is not { IsGroupable: true })
                .Select(key => Violation($"Report '{report.ReportKey}' breaks out by default by '{key}', which is no groupable calculation dimension of the report version.")));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

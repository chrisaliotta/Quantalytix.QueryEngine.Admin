using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_defaults_resolve</c>: the calculation dimensions the report's default filter compares are filterable, and those its
/// default pivot breaks out by are groupable, enabled calculation dimensions of the report; and each default comparison runs under
/// an enabled profile of the report, names a scenario its scenario dimension's published version holds, and reads a captured
/// assumption set. Every request that takes the defaults reads them.
/// </summary>
public sealed class ReportDefaultsResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_defaults_resolve";

    /// <summary>Reports each default dimension the report cannot serve, and each key a default comparison names that does not resolve.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the scenario dimension's published version and the captured assumption sets.</param>
    /// <returns>One violation per unserved dimension or unresolved key.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var available = report.EnabledDimensions.ToDictionary(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        foreach (var key in ReportDefaults.FilterDimensions(report.Report.DefaultFilterJson).Where(key => available.GetValueOrDefault(key) is not { IsFilterable: true }))
            yield return Violation($"Report '{report.ReportKey}' filters by default on '{key}', which is no filterable calculation dimension of the report.");
        foreach (var key in ReportDefaults.PivotDimensions(report.Report.DefaultPivotJson).Where(key => available.GetValueOrDefault(key) is not { IsGroupable: true }))
            yield return Violation($"Report '{report.ReportKey}' breaks out by default by '{key}', which is no groupable calculation dimension of the report.");

        var profiles = report.Profiles.Where(profile => profile.IsEnabled).Select(profile => profile.ProfileKey).ToHashSet(StringComparer.Ordinal);
        var scenarioDimension = report.EnabledDimensions.Where(dimension => dimension.IsScenario).MinBy(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        foreach (var comparison in ReportComparisons.Of(report.Report.DefaultSettingsJson))
        {
            var subject = $"Report '{report.ReportKey}' default comparison '{comparison.Key}'";
            if (comparison.ProfileKey is { } profileKey && !profiles.Contains(profileKey))
                yield return Violation($"{subject} runs under profile '{profileKey}', which is no enabled profile of the report.");
            if (comparison.Scenario is { } scenario && ScenarioProblem(scenario, scenarioDimension, scope) is { } problem)
                yield return Violation($"{subject} {problem}");
            if (comparison.AssumptionSet is { } set && !scope.HistoricalSetKeys.Contains(set))
                yield return Violation($"{subject} reads assumption set '{set}', which is no captured assumption set.");
        }
    }

    /// <summary>Why a scenario does not resolve in the published version of the report's scenario dimension; null when it does.</summary>
    private static string? ScenarioProblem(ScenarioSelection scenario, CalculationDimensionRecord? dimension, ReportScope scope)
    {
        if (dimension is null) return $"names scenario '{scenario.Key}', but the report has no scenario dimension.";
        if (scope.Templates[dimension.RoleId] is not { } keys)
            return $"names scenario '{scenario.Key}', but scenario dimension '{dimension.DimensionKey}' reads a dimension with no published version.";
        var version = $"v{keys.Version.VersionNumber}, the published version of scenario dimension '{dimension.DimensionKey}'";
        if (!scenario.Latest)
            return keys.IsAssignable(scenario.Key) ? null : $"names scenario '{scenario.Key}', which is no assignable member of {version}.";
        return keys.HasAssignableBelow(scenario.Key)
            ? null
            : $"names the latest scenario under folder '{scenario.Key}', which has no assignable member under it in {version}.";
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}

using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_defaults_resolve</c>: the calculation dimensions the report's default filter compares are filterable, and those its
/// default pivot breaks out by are groupable, enabled calculation dimensions of the report, because every request that takes the
/// defaults reads them. No request parameter the default filter compares is named like a parameter of the report's model, because
/// every request must supply the filter's parameters and the engine refuses a request that supplies a model parameter, whose values
/// the report's rows set.
/// </summary>
public sealed class ReportDefaultsResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_defaults_resolve";

    /// <summary>Reports each default dimension that the report cannot serve, and each default filter parameter no request could supply.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: calculation dimensions are the report's own, and parameters its model's.</param>
    /// <returns>One violation per unserved dimension or unsuppliable parameter.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var available = report.EnabledDimensions.ToDictionary(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        foreach (var key in ReportDefaults.FilterDimensions(report.Report.DefaultFilterJson).Where(key => available.GetValueOrDefault(key) is not { IsFilterable: true }))
            yield return Violation($"Report '{report.ReportKey}' filters by default on '{key}', which is no filterable calculation dimension of the report.");
        foreach (var key in ReportDefaults.PivotDimensions(report.Report.DefaultPivotJson).Where(key => available.GetValueOrDefault(key) is not { IsGroupable: true }))
            yield return Violation($"Report '{report.ReportKey}' breaks out by default by '{key}', which is no groupable calculation dimension of the report.");
        var declared = report.Parameters.Select(parameter => parameter.ParameterKey).ToHashSet(StringComparer.Ordinal);
        foreach (var name in ReportDefaults.FilterParameters(report.Report.DefaultFilterJson).Where(declared.Contains))
            yield return Violation($"Report '{report.ReportKey}' filters by default on @{name}, a parameter of its model, which its rows set and so no request can supply.");
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}

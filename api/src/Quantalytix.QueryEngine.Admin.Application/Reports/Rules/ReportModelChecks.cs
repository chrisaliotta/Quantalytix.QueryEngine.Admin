using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The reports module's hook for the model gate (C7): checks the reports over a model against the model's content, as a model change
/// set would leave it, in place of the reports' own copy of it. Every report, enabled or not, must still set only parameters the
/// model declares, as their types and allowed values say; every enabled report must still find the metrics it names, under each
/// enabled profile a source for each variable it reads, and compare no parameter of the model in its default filter.
/// </summary>
public static class ReportModelChecks
{
    /// <summary>The report rule that reads a model's parameters, which every report's row values keep passing.</summary>
    private static readonly IReportRule ParameterRule = new ReportRowParametersResolveRule();

    /// <summary>The report rules that read a model's metrics and parameter names, which every enabled report keeps passing.</summary>
    private static readonly IReportRule[] EnabledReportRules = [new ReportMetricsResolveRule(), new ProfilesCompleteRule(), new ReportDefaultsResolveRule()];

    /// <summary>The violations of the reports over a model with the model's content.</summary>
    /// <param name="reports">The reports that read the model, enabled or not.</param>
    /// <param name="scope">What the reports are checked against besides their content.</param>
    /// <param name="model">The model's content.</param>
    /// <returns>The parameter rule's violations over every report, then the enabled reports' metric rules'.</returns>
    public static IEnumerable<RuleViolation> Violations(IEnumerable<ReportGraph> reports, ReportScope scope, ModelGraph model)
    {
        var withModel = reports
            .Select(report => report with { Variables = model.Variables, CalculatedMeasures = model.CalculatedMeasures, Parameters = model.Parameters })
            .ToList();
        return withModel.SelectMany(report => ParameterRule.Evaluate(report, scope))
            .Concat(withModel.Where(report => report.Report.IsEnabled).SelectMany(report => EnabledReportRules.SelectMany(rule => rule.Evaluate(report, scope))));
    }
}

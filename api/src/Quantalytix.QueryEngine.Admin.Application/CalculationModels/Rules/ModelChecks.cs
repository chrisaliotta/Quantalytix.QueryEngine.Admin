using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Every check a model passes, because the engine reads it at once through the reports over it (C7): the model's formulas must
/// resolve, stay acyclic, fit its formula language and read each parameter where its type belongs, and its attributions must resolve
/// and partition their expressions; every report of the model, enabled or not, must still set only parameters the model declares, as
/// their types and allowed values say; and every enabled report must still find the metrics it names, under each enabled profile a
/// source for each variable it reads, and compare no parameter of the model in its default filter. The report rules run on each
/// report with the model's content in place of the report's own copy of it.
/// </summary>
public static class ModelChecks
{
    /// <summary>The rules every model passes on its own, in reporting order.</summary>
    private static readonly Func<ModelGraph, IEnumerable<RuleViolation>>[] ModelRules =
    [
        FormulaReferencesResolveRule.Evaluate,
        FormulasAcyclicRule.Evaluate,
        FormulasFitLanguageRule.Evaluate,
        FormulaParametersResolveRule.Evaluate,
        AttributionReferencesResolveRule.Evaluate,
        AttributionDriversRule.Evaluate,
        AttributionWeightVariableRule.Evaluate
    ];

    /// <summary>The report rule that reads a model's parameters, which every report's row values keep passing.</summary>
    private static readonly IReportRule ParameterRule = new ReportRowParametersResolveRule();

    /// <summary>The report rules that read a model's metrics and parameter names, which every enabled report keeps passing.</summary>
    private static readonly IReportRule[] EnabledReportRules = [new ReportMetricsResolveRule(), new ProfilesCompleteRule(), new ReportDefaultsResolveRule()];

    /// <summary>The violations of a model and of the reports over it.</summary>
    /// <param name="snapshot">The model, stored or as a change set leaves it.</param>
    /// <returns>The model rules' violations, then the report rules'.</returns>
    public static IReadOnlyList<RuleViolation> Violations(ModelSnapshot snapshot)
    {
        var content = snapshot.Content;
        List<RuleViolation> violations = [.. ModelRules.SelectMany(rule => rule(content))];
        if (snapshot.Surroundings.Scope is not { } scope) return violations;

        var reports = snapshot.Surroundings.Reports
            .Select(report => report with { Variables = content.Variables, CalculatedMeasures = content.CalculatedMeasures, Parameters = content.Parameters })
            .ToList();
        violations.AddRange(reports.SelectMany(report => ParameterRule.Evaluate(report, scope)));
        violations.AddRange(reports.Where(report => report.Report.IsEnabled).SelectMany(report => EnabledReportRules.SelectMany(rule => rule.Evaluate(report, scope))));
        return violations;
    }
}

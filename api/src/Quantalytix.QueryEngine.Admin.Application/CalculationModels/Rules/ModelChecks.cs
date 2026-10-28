using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Every check a model passes, because the engine reads it at once through the reports over it (C7): the model's formulas must
/// resolve, stay acyclic, fit its formula language and read each parameter where its type belongs, and its attributions must resolve
/// and partition their expressions; every report of the model, enabled or not, must still set only parameters the model declares, as
/// their types and allowed values say; and every enabled report must still find the metrics it names, under each enabled profile a
/// source for each variable it reads, and compare no parameter of the model in its default filter. The reports module's
/// <see cref="ReportModelChecks"/> runs those report rules on each report with the model's content in place of the report's own copy.
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

    /// <summary>The violations of a model and of the reports over it.</summary>
    /// <param name="snapshot">The model, stored or as a change set leaves it.</param>
    /// <returns>The model rules' violations, then the report rules'.</returns>
    public static IReadOnlyList<RuleViolation> Violations(ModelSnapshot snapshot)
    {
        var content = snapshot.Content;
        List<RuleViolation> violations = [.. ModelRules.SelectMany(rule => rule(content))];
        if (snapshot.Surroundings.Scope is not { } scope) return violations;

        violations.AddRange(ReportModelChecks.Violations(snapshot.Surroundings.Reports, scope, content));
        return violations;
    }
}

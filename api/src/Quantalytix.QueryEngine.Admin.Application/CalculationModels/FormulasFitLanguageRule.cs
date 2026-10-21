using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formulas_fit_language</c>: the model's formulas and attribution expressions call only the functions of its formula language,
/// so <c>POWER</c> needs language 1.1. The engine evaluates a model's formulas in the model's language, and the language can change
/// after a formula is written, so the rule checks every formula on every write.
/// </summary>
public static class FormulasFitLanguageRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formulas_fit_language";

    /// <summary>Reports each formula and attribution expression that calls POWER in a model of language 1.0.</summary>
    /// <param name="model">The model, its metrics and attributions.</param>
    /// <returns>One violation per formula that calls POWER while the model's language lacks it.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        if (model.Model.FormulaLanguageVersion == FormulaLanguageVersions.Version11) return [];
        var formulas = model.CalculatedMeasures
            .Select(measure => (Subject: $"Calculated measure '{measure.MeasureKey}'", Expression: measure.ExpressionText))
            .Concat(model.Attributions
                .Where(attribution => attribution.ExpressionText is not null)
                .Select(attribution => (Subject: $"The attribution of '{attribution.MetricKey}'", Expression: attribution.ExpressionText!)));
        return formulas
            .Where(formula => ExpressionInspector.Inspect(formula.Expression).Functions.Contains(ExpressionFunction.Power))
            .OrderBy(formula => formula.Subject, StringComparer.Ordinal)
            .Select(formula => new RuleViolation
            {
                Rule = Code,
                Message = $"{formula.Subject} calls POWER, which formula language {model.Model.FormulaLanguageVersion} lacks; the model needs formula language {FormulaLanguageVersions.Version11}."
            });
    }
}

using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formula_references_resolve</c>: every <c>[m.KEY]</c> of every calculated measure names a variable or calculated measure of the
/// model. The report orchestrator evaluates formulas against the model's keys alone, so a key that an edit renamed or deleted would
/// have nothing to read.
/// </summary>
public static class FormulaReferencesResolveRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formula_references_resolve";

    /// <summary>Reports each formula reference that names no key of the model.</summary>
    /// <param name="model">The model's metrics.</param>
    /// <returns>One violation per unresolved reference.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var keys = model.MetricKeys;
        return model.CalculatedMeasures.OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal).SelectMany(measure =>
            Unresolved(keys, measure.ExpressionText).Select(key => new RuleViolation
            {
                Code = Code,
                Message = $"Calculated measure '{measure.MeasureKey}' reads [m.{key}], which is no variable or calculated measure of the model."
            }));
    }

    /// <summary>The <c>[m.KEY]</c> references of a model formula that name no metric of the model.</summary>
    /// <param name="metricKeys">The model's <see cref="ModelGraph.MetricKeys"/>.</param>
    /// <param name="expressionText">A calculated measure's formula or an attribution expression.</param>
    /// <returns>The unresolved keys, in reading order.</returns>
    public static IEnumerable<string> Unresolved(IReadOnlySet<string> metricKeys, string expressionText)
    {
        return ExpressionInspector.Inspect(expressionText).MeasureKeys.Where(key => !metricKeys.Contains(key));
    }
}

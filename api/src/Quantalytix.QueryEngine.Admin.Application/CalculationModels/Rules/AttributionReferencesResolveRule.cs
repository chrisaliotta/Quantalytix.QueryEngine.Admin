using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>attribution_references_resolve</c>: every attribution names a metric of its model, a variable's attribution has an expression,
/// because a variable has no formula to switch drivers in, and every <c>[m.KEY]</c> of an attribution expression and every input a
/// driver switches names a metric of the model, or <c>@periodDays</c> or <c>@yearDays</c>. The engine evaluates attribution
/// expressions against the model's keys alone, as it does formulas.
/// </summary>
public static class AttributionReferencesResolveRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "attribution_references_resolve";

    /// <summary>Reports each attribution of a missing metric, each variable's attribution without an expression, and each unresolved key.</summary>
    /// <param name="model">The model's metrics and attributions.</param>
    /// <returns>One violation per problem.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var keys = model.MetricKeys;
        var variableKeys = model.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        foreach (var attribution in model.Attributions.OrderBy(attribution => attribution.MetricKey, StringComparer.Ordinal))
        {
            var subject = $"The attribution of '{attribution.MetricKey}'";
            if (!keys.Contains(attribution.MetricKey))
                yield return Violation($"{subject} names no variable or calculated measure of the model.");
            else if (attribution.ExpressionText is null && variableKeys.Contains(attribution.MetricKey))
                yield return Violation($"{subject} needs an expression, because a variable has no formula to switch its drivers in.");
            if (attribution.ExpressionText is { } expression)
            {
                foreach (var key in FormulaReferencesResolveRule.Unresolved(keys, expression))
                    yield return Violation($"{subject} reads [m.{key}], which is no variable or calculated measure of the model.");
            }

            foreach (var driver in attribution.Drivers)
            {
                foreach (var input in driver.InputKeys.Where(input => !keys.Contains(input) && !AttributionInputs.ContextParameters.Contains(input)))
                    yield return Violation($"{subject} has driver '{driver.DriverKey}' switch '{input}', which is no variable or calculated measure of the model, @periodDays or @yearDays.");
            }
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}

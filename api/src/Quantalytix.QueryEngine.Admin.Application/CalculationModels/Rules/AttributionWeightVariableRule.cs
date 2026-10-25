using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>attribution_weight_variable</c>: the variable an attribution weighs members by is a variable of the model, because the engine
/// splits a ratio's change across child rows and breakout members by each member's value of that variable.
/// </summary>
public static class AttributionWeightVariableRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "attribution_weight_variable";

    /// <summary>Reports each weight variable that is no variable of the model.</summary>
    /// <param name="model">The model's metrics and attributions.</param>
    /// <returns>One violation per unresolved weight variable.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var variableKeys = model.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        return model.Attributions
            .Where(attribution => attribution.WeightVariableKey is { } weight && !variableKeys.Contains(weight))
            .OrderBy(attribution => attribution.MetricKey, StringComparer.Ordinal)
            .Select(attribution => new RuleViolation
            {
                Rule = Code,
                Message = $"The attribution of '{attribution.MetricKey}' weighs members by '{attribution.WeightVariableKey}', which is no variable of the model."
            });
    }
}

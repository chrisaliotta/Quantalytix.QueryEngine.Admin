using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>attribution_drivers</c>: a metric's drivers partition the inputs of its attribution expression, so every input switches in
/// exactly one driver and no driver switches an input the expression does not read; their keys are distinct, because a key names
/// an expanded report column; and a metric has one to eight drivers, its own or, without any, the default ones, because Shapley
/// attribution evaluates every subset of them.
/// </summary>
public static class AttributionDriversRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "attribution_drivers";

    /// <summary>Reports each attribution whose drivers do not partition its expression's inputs, repeat a key, or are too many or none.</summary>
    /// <param name="model">The model's metrics and attributions.</param>
    /// <returns>One violation per problem.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        foreach (var attribution in model.Attributions.OrderBy(attribution => attribution.MetricKey, StringComparer.Ordinal))
        {
            if (AttributionInputs.ExpressionOf(model, attribution) is not { } expression) continue;
            var subject = $"The attribution of '{attribution.MetricKey}'";
            var target = ModelAreas.Attribution(attribution.MetricKey);
            if (attribution.Drivers.Count == 0)
            {
                var count = AttributionInputs.DefaultDrivers(model, expression).Count;
                if (count is < 1 or > AttributionInputs.MaxDrivers)
                    yield return Violation($"{subject} has {count} default drivers, one per metric its expression reads and one for its days; a metric has 1 to {AttributionInputs.MaxDrivers}.", target);
                continue;
            }

            if (attribution.Drivers.Count > AttributionInputs.MaxDrivers)
                yield return Violation($"{subject} has {attribution.Drivers.Count} drivers; a metric has at most {AttributionInputs.MaxDrivers}.", target);
            foreach (var key in attribution.Drivers.GroupBy(driver => driver.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
                yield return Violation($"{subject} has more than one driver '{key}'.", target);

            var inputs = AttributionInputs.Of(expression);
            var switching = attribution.Drivers
                .SelectMany(driver => driver.Inputs.Select(input => (Input: input, driver.Key)))
                .ToLookup(pair => pair.Input, pair => pair.Key, StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                var drivers = switching[input].ToList();
                if (drivers.Count == 0) yield return Violation($"{subject} reads '{input}', which no driver switches.", target);
                else if (drivers.Count > 1) yield return Violation($"{subject} reads '{input}', which more than one driver switches: {string.Join(", ", drivers.Select(key => $"'{key}'"))}.", target);
            }

            foreach (var outside in switching.Select(group => group.Key).Where(input => !inputs.Contains(input)))
                yield return Violation($"{subject} has drivers that switch '{outside}', which its expression does not read.", target);
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message, ViolationTarget target)
    {
        return new RuleViolation { Code = Code, Message = message, Target = target };
    }
}

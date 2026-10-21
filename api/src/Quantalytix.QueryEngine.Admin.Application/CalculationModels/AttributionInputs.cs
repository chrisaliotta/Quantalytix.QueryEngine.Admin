using Quantalytix.QueryEngine.Admin.Application.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The inputs a metric's attribution switches between the two values compared: the <c>[m.KEY]</c> references of its expression, by
/// key, and its context parameters, as <c>@periodDays</c> and <c>@yearDays</c>. A driver names its inputs the same way.
/// </summary>
public static class AttributionInputs
{
    /// <summary>The most drivers a metric has, because Shapley attribution evaluates every subset of them.</summary>
    public const int MaxDrivers = 8;

    /// <summary>The context parameters an input may name besides the model's metrics, which the default drivers switch as one.</summary>
    public static readonly IReadOnlySet<string> ContextParameters = new HashSet<string>(["@periodDays", "@yearDays"], StringComparer.Ordinal);

    /// <summary>The expression a metric's drivers are switched in: the attribution's own, or else the calculated measure's formula.</summary>
    /// <param name="model">The model.</param>
    /// <param name="attribution">One of its attributions.</param>
    /// <returns>The expression; null for a variable without one, or a metric the model lacks.</returns>
    public static string? ExpressionOf(ModelGraph model, MetricAttribution attribution)
    {
        return attribution.ExpressionText
            ?? model.CalculatedMeasures.FirstOrDefault(measure => measure.MeasureKey == attribution.MetricKey)?.ExpressionText;
    }

    /// <summary>The distinct inputs of an attribution expression, in reading order: its metric keys, then its context parameters.</summary>
    /// <param name="expressionText">An attribution expression or calculated measure formula.</param>
    /// <returns>The inputs, as drivers name them.</returns>
    public static IReadOnlyList<string> Of(string expressionText)
    {
        var inspection = ExpressionInspector.Inspect(expressionText);
        return [.. inspection.MeasureKeys, .. inspection.ParameterNames.Select(name => $"@{name}")];
    }

    /// <summary>How many default drivers an expression has: one per metric it reads, and one <c>days</c> driver for its context parameters.</summary>
    /// <param name="inputs">The expression's inputs.</param>
    /// <returns>The number of default drivers.</returns>
    public static int DefaultDriverCount(IReadOnlyList<string> inputs)
    {
        return inputs.Count(input => !ContextParameters.Contains(input)) + (inputs.Any(ContextParameters.Contains) ? 1 : 0);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The inputs a metric's attribution switches between the two values compared: the <c>[m.KEY]</c> references of its expression, by
/// key, and its days, as <c>@periodDays</c> and <c>@yearDays</c>. A driver names its inputs the same way. The model's declared
/// parameters are never inputs: a row's value is the same on both sides of a comparison.
/// </summary>
public static class AttributionInputs
{
    /// <summary>The most drivers a metric has, because Shapley attribution evaluates every subset of them.</summary>
    public const int MaxDrivers = 8;

    /// <summary>The days inputs, which the default drivers switch as one, in the order the <c>days</c> driver lists them.</summary>
    public static readonly IReadOnlyList<string> DaysInputs = [.. FormulaContext.ParameterNames.Select(name => $"@{name}")];

    /// <summary>The context parameters an input may name besides the model's metrics, which the default drivers switch as one.</summary>
    public static readonly IReadOnlySet<string> ContextParameters = DaysInputs.ToHashSet(StringComparer.Ordinal);

    /// <summary>The expression a metric's drivers are switched in: the attribution's own, or else the calculated measure's formula.</summary>
    /// <param name="model">The model.</param>
    /// <param name="attribution">One of its attributions.</param>
    /// <returns>The expression; null for a variable without one, or a metric the model lacks.</returns>
    public static string? ExpressionOf(ModelGraph model, MetricAttribution attribution)
    {
        return attribution.ExpressionText
            ?? model.CalculatedMeasures.FirstOrDefault(measure => measure.MeasureKey == attribution.MetricKey)?.ExpressionText;
    }

    /// <summary>
    /// The distinct inputs of an attribution expression: its metric keys in reading order, then both days inputs when it reads either
    /// or calls <c>YEARFRAC</c>, which reads the period; the days it reads come first, in reading order.
    /// </summary>
    /// <param name="expressionText">An attribution expression or calculated measure formula.</param>
    /// <returns>The inputs, as drivers name them.</returns>
    public static IReadOnlyList<string> Of(string expressionText)
    {
        var inspection = ExpressionInspector.Inspect(expressionText);
        string[] days = [.. inspection.ParameterNames.Select(name => $"@{name}").Where(ContextParameters.Contains)];
        var readsDays = days.Length > 0 || inspection.Functions.Contains(ExpressionFunction.YearFrac);
        return [.. inspection.MeasureKeys, .. readsDays ? days.Union(DaysInputs, StringComparer.Ordinal) : []];
    }

    /// <summary>How many default drivers an expression has: one per metric it reads, and one <c>days</c> driver for its days.</summary>
    /// <param name="inputs">The expression's inputs.</param>
    /// <returns>The number of default drivers.</returns>
    public static int DefaultDriverCount(IReadOnlyList<string> inputs)
    {
        return inputs.Count(input => !ContextParameters.Contains(input)) + (inputs.Any(ContextParameters.Contains) ? 1 : 0);
    }
}

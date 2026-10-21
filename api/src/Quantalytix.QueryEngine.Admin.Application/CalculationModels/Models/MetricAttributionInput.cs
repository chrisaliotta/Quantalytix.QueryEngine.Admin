namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A metric's attribution with its drivers, as <c>PUT calculation-models/{modelId}/attributions/{metricKey}</c> takes it.</summary>
public sealed record MetricAttributionInput
{
    /// <summary>
    /// The expression the drivers are switched in, over <c>[m.KEY]</c> references and <c>@periodDays</c> and <c>@yearDays</c>;
    /// null keeps a calculated measure's own formula, and a variable needs one.
    /// </summary>
    public string? ExpressionText { get; init; }

    /// <summary>The key of the variable that weighs members for the metric's contributions; null for none.</summary>
    public string? WeightVariableKey { get; init; }

    /// <summary>Every driver of the metric, at most eight; none for the default drivers.</summary>
    public required IReadOnlyList<AttributionDriver> Drivers { get; init; }
}

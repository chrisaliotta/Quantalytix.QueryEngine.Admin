namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// How a change in one metric of a model, between two values such as a month and the month before, splits into the effects of its
/// drivers, with the drivers it switches. Without drivers, each reference of the expression is a driver of its own, and
/// <c>@periodDays</c> with <c>@yearDays</c> together form one <c>days</c> driver.
/// </summary>
public sealed record MetricAttribution
{
    /// <summary>The key of the variable or calculated measure attributed.</summary>
    public required string MetricKey { get; init; }

    /// <summary>The expression the drivers are switched in; null means the calculated measure's own formula.</summary>
    public string? ExpressionText { get; init; }

    /// <summary>The variable that weighs child rows and members when a ratio's change splits into rate, mix and interaction.</summary>
    public string? WeightVariableKey { get; init; }

    /// <summary>The drivers, by sort order; none for the default drivers.</summary>
    public required IReadOnlyList<AttributionDriver> Drivers { get; init; }
}

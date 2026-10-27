namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>Attribution</c>: how a change in one metric, between two values such as a month and the month before, splits
/// into the effects of its drivers (<c>qe_metric_attribution</c> with its drivers). A metric without one uses the default drivers:
/// one per metric its formula reads and one <c>days</c> driver for <c>@periodDays</c> and <c>@yearDays</c> (C6).
/// </summary>
public sealed record Attribution
{
    /// <summary>The key of the variable or calculated measure attributed.</summary>
    public required string MetricKey { get; init; }

    /// <summary>The expression the drivers are switched in; required for a variable, null for a measure's own formula.</summary>
    public required string? Expression { get; init; }

    /// <summary>The variable that weighs child rows and members when a ratio's change splits into rate, mix and interaction.</summary>
    public required string? WeightVariableKey { get; init; }

    /// <summary>The drivers in the order sequential attribution switches them; none for the default drivers.</summary>
    public required IReadOnlyList<AttributionDriver> Drivers { get; init; }
}

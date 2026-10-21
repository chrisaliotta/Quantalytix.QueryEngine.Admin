namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// One driver of a metric's attribution, as the attribution endpoints take and return it: inputs of the attribution expression that
/// switch together between the two values compared.
/// </summary>
public sealed record AttributionDriver
{
    /// <summary>Key, unique among the metric's drivers and never <c>residual</c>; it names the driver's expanded report column.</summary>
    public required string DriverKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The order sequential attribution switches the drivers in, unique among the metric's drivers.</summary>
    public required int SortOrder { get; init; }

    /// <summary>The metric keys and <c>@periodDays</c> or <c>@yearDays</c> parameters of the expression that switch together.</summary>
    public required IReadOnlyList<string> InputKeys { get; init; }
}

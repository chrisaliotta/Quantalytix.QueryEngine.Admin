namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>AttributionDriver</c>: inputs of an attribution expression that switch together between the two values
/// compared. Its position among the metric's drivers is its <c>sort_order</c>.
/// </summary>
public sealed record AttributionDriver
{
    /// <summary>Key, unique among the metric's drivers and never <c>residual</c>; it names the driver's expanded report column.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The metric keys and <c>@periodDays</c> or <c>@yearDays</c> of the expression that switch together (<c>input_keys_json</c>).</summary>
    public required IReadOnlyList<string> Inputs { get; init; }
}

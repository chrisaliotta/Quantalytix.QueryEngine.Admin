namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>ModelVariable</c>: an input metric each report fills from its sources (<c>qe_variable</c>). Its key shares the
/// model's metric namespace with the calculated measures (C1).
/// </summary>
public sealed record ModelVariable
{
    /// <summary>Key, unique among the model's variables and calculated measures, fixed once saved.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Presentation hint such as <c>currency</c>, which report columns show unless they override it.</summary>
    public required string DisplayFormat { get; init; }

    /// <summary>Optional unit, such as a currency code.</summary>
    public required string? UnitKey { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value.</summary>
    public required string MissingBehavior { get; init; }
}

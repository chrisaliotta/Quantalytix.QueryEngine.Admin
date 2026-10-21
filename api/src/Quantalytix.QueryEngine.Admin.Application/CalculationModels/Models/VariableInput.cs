namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A variable of a calculation model, as its create and edit endpoints take it.</summary>
public sealed record VariableInput
{
    /// <summary>Key, unique among the model's variables and calculated measures.</summary>
    public required string VariableKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Free-form classification such as <c>average_balance</c>.</summary>
    public required string VariableRole { get; init; }

    /// <summary>A <c>variable_missing_behavior</c> value.</summary>
    public required string MissingBehavior { get; init; }

    /// <summary>Presentation hint such as <c>number</c>, <c>percent</c> or <c>currency</c>, which report columns show by default.</summary>
    public required string DisplayFormat { get; init; }

    /// <summary>Optional unit, such as a currency code.</summary>
    public string? UnitKey { get; init; }
}

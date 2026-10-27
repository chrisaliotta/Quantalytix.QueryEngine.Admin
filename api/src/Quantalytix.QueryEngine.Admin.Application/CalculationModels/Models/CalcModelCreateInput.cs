namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The contract's <c>CalcModelCreateInput</c>: a new model, which is created disabled and empty.</summary>
public sealed record CalcModelCreateInput
{
    /// <summary>The model's key, unique across models and fixed once created.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The ISO 4217 currency of the model's monetary figures.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>A <c>formula_language_version</c> value.</summary>
    public required string FormulaLanguage { get; init; }

    /// <summary>An <c>attribution_method</c> value.</summary>
    public required string AttributionMethod { get; init; }
}

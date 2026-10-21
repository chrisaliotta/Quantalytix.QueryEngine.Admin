namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The editable attributes of a calculation model, as <c>PUT calculation-models/{modelId}</c> takes them.</summary>
public record CalculationModelInput
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The ISO 4217 currency of the model's monetary figures, such as <c>USD</c>.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>A <c>formula_language_version</c> value: <c>1.0</c>, or <c>1.1</c>, whose formulas may call <c>POWER</c>.</summary>
    public required string FormulaLanguageVersion { get; init; }

    /// <summary>An <c>attribution_method</c> value: <c>shapley</c> or <c>sequential</c>.</summary>
    public required string AttributionMethod { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A parameter of a calculation model, as <c>PUT calculation-models/{modelId}/parameters/{parameterKey}</c> takes it.</summary>
public sealed record ModelParameterInput
{
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>parameter_value_type</c> value: <c>number</c> or <c>text</c>.</summary>
    public required string ValueType { get; init; }

    /// <summary>The texts a text parameter may take, each once; null for any text, and for a number parameter.</summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>A number parameter's default, which fits <c>DECIMAL(38, 12)</c>; null for a text parameter.</summary>
    public decimal? DefaultNumber { get; init; }

    /// <summary>A text parameter's default, one of its allowed values when it has them; null for a number parameter.</summary>
    public string? DefaultText { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }
}

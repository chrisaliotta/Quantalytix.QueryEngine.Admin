namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_model_parameter</c> row: a value a calculation model declares for its formulas to read as <c>@key</c>, which each report
/// sets on its rows. A row without a value of its own takes its nearest ancestor's, and else the default.
/// </summary>
public sealed record ModelParameterRecord
{
    /// <summary>The owning model; first part of the primary key.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>The key formulas read the parameter by, as <c>@key</c>; second part of the primary key.</summary>
    public required string ParameterKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>parameter_value_type</c> value.</summary>
    public required string ValueType { get; init; }

    /// <summary>A JSON array of the texts a text parameter may take; null when it may take any text, and for a number parameter.</summary>
    public string? AllowedValuesJson { get; init; }

    /// <summary>The default of a number parameter; null for a text parameter.</summary>
    public decimal? DefaultNumber { get; init; }

    /// <summary>The default of a text parameter; null for a number parameter.</summary>
    public string? DefaultText { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }
}

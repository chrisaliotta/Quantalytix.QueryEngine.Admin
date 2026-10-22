namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// A value a calculation model declares for its formulas to read as <c>@key</c>, as the parameter endpoints return it. Each report
/// sets values on its rows; a row without one of its own takes its nearest ancestor's, and else the default.
/// </summary>
public sealed record ModelParameter
{
    /// <summary>The key formulas read the parameter by, as <c>@key</c>; never <c>periodDays</c> or <c>yearDays</c>.</summary>
    public required string ParameterKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>parameter_value_type</c> value: <c>number</c> or <c>text</c>.</summary>
    public required string ValueType { get; init; }

    /// <summary>The texts a text parameter may take; null when it may take any text, and for a number parameter.</summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>The value of a number parameter on the rows no row above sets; null for a text parameter.</summary>
    public decimal? DefaultNumber { get; init; }

    /// <summary>The value of a text parameter on the rows no row above sets; null for a number parameter.</summary>
    public string? DefaultText { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }
}

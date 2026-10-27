namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The contract's <c>ModelParameter</c> (<c>qe_model_parameter</c>, language 1.2): it sets exactly the default its type names; only
/// text parameters have allowed values, and they include the default (C5). Named apart from <see cref="ModelParameter"/>, the
/// form the report rules read.
/// </summary>
public sealed record ModelParameterItem
{
    /// <summary>The key formulas read the parameter by, as <c>@key</c>; never <c>periodDays</c> or <c>yearDays</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>parameter_value_type</c> value: <c>number</c> or <c>text</c>.</summary>
    public required string ValueType { get; init; }

    /// <summary>The texts a text parameter may take; null for any text, and for a number parameter.</summary>
    public required IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>A number parameter's default; null for a text parameter.</summary>
    public required decimal? DefaultNumber { get; init; }

    /// <summary>A text parameter's default; null for a number parameter.</summary>
    public required string? DefaultText { get; init; }

    /// <summary>Optional description.</summary>
    public required string? Description { get; init; }
}

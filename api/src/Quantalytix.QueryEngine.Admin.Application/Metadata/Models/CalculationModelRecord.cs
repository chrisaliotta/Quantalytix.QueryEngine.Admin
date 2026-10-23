namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_model</c> row: a calculation model, whose variables and calculated measures reports read.</summary>
public sealed record CalculationModelRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>Stable logical model identity.</summary>
    public required string ModelKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The calendar its periods follow; <c>gregorian</c>.</summary>
    public required string CalendarKey { get; init; }

    /// <summary>The ISO 4217 currency of its monetary figures.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>A <c>formula_language_version</c> value: the expression grammar its formulas and attribution expressions use.</summary>
    public required string FormulaLanguageVersion { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>An <c>attribution_method</c> value: how a metric's change between two values splits into the effects of its drivers.</summary>
    public required string AttributionMethod { get; init; }
}

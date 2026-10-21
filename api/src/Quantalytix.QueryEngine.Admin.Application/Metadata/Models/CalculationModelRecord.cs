namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_model</c> row: a calculation model, whose variables and calculated measures reports read.</summary>
public sealed record CalculationModelRecord
{
    /// <summary>Primary key.</summary>
    public required long ModelId { get; init; }

    /// <summary>Stable logical model identity.</summary>
    public required string ModelKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The calendar its periods follow; <c>gregorian</c>.</summary>
    public required string CalendarKey { get; init; }

    /// <summary>The ISO 4217 currency of its monetary figures.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>The expression grammar version its formulas use.</summary>
    public required string FormulaLanguageVersion { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_model_version</c> row: one immutable version of a calculation model: its variables, sources, profiles, formulas and calculation dimensions.</summary>
public sealed record CalculationModelVersionRecord
{
    /// <summary>Primary key.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>The owning model.</summary>
    public required long ModelId { get; init; }

    /// <summary>Version number within the model.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>A <c>publication_status</c> value.</summary>
    public required string Status { get; init; }

    /// <summary>The calendar its periods follow; <c>gregorian</c>.</summary>
    public required string CalendarKey { get; init; }

    /// <summary>The ISO 4217 currency of its monetary figures.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>The expression grammar version its formulas use.</summary>
    public required string FormulaLanguageVersion { get; init; }

    /// <summary>Publication time (UTC).</summary>
    public DateTime? PublishedAt { get; init; }
}

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_calculation_model</c> row: the stable identity of a calculation model, which reports read their figures from.</summary>
public sealed record CalculationModelRecord
{
    /// <summary>Primary key.</summary>
    public required long ModelId { get; init; }

    /// <summary>Stable logical model identity.</summary>
    public required string ModelKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability flag.</summary>
    public required bool IsEnabled { get; init; }
}

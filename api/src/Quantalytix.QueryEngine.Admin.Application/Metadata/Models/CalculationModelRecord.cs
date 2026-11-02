namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_calculation_model</c> row: a calculation model's identity. Its settings, variables and calculated measures are versioned
/// per snapshot in <c>qe_calculation_model_version</c>.
/// </summary>
public sealed record CalculationModelRecord
{
    /// <summary>Primary key.</summary>
    public required long CalculationModelId { get; init; }

    /// <summary>Stable logical model identity.</summary>
    public required string ModelKey { get; init; }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A calculation model's metrics, as they stand or as a write would leave them: what <see cref="ModelChanges"/> checks.</summary>
public sealed record ModelGraph
{
    /// <summary>The model's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }
}

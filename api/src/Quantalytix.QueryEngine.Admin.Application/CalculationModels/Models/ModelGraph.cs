using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A calculation model with its metrics, as they stand or as a write would leave them: what <see cref="ModelChanges"/> checks.</summary>
public sealed record ModelGraph
{
    /// <summary>The model, with its formula language and attribution method.</summary>
    public required CalculationModelRecord Model { get; init; }

    /// <summary>The model's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The attributions of the model's metrics, with their drivers.</summary>
    public required IReadOnlyList<MetricAttribution> Attributions { get; init; }

    /// <summary>The keys every <c>[m.KEY]</c> of the model may name: its variables' and calculated measures'.</summary>
    public IReadOnlySet<string> MetricKeys =>
        Variables.Select(variable => variable.VariableKey).Concat(CalculatedMeasures.Select(measure => measure.MeasureKey)).ToHashSet(StringComparer.Ordinal);
}

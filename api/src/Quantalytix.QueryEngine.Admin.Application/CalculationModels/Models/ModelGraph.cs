using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// A calculation model's content, as stored or as a change set leaves it: the model row, its metrics, their attributions and its
/// parameters. The model rules read it alone; the report rules read its metrics and parameters in place of the reports' own.
/// </summary>
public sealed record ModelGraph
{
    /// <summary>The model, with its formula language and attribution method.</summary>
    public required CalculationModelRecord Model { get; init; }

    /// <summary>The model's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The explicit attributions of the model's metrics, with their drivers.</summary>
    public required IReadOnlyList<Attribution> Attributions { get; init; }

    /// <summary>The parameters the model declares for its formulas.</summary>
    public required IReadOnlyList<ModelParameter> Parameters { get; init; }

    /// <summary>The model's variable keys.</summary>
    public IReadOnlySet<string> VariableKeys => Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);

    /// <summary>The keys every <c>[m.KEY]</c> of the model may name: its variables' and calculated measures'.</summary>
    public IReadOnlySet<string> MetricKeys =>
        Variables.Select(variable => variable.VariableKey).Concat(CalculatedMeasures.Select(measure => measure.MeasureKey)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every formula the engine evaluates for the model, with the subject rule messages name it by and where to fix it: each
    /// calculated measure's, and each attribution expression; an attribution without one switches its drivers in the formula.
    /// </summary>
    public IEnumerable<(string Subject, string Expression, ViolationTarget Target)> Formulas =>
        CalculatedMeasures
            .Select(measure => (Subject: $"Calculated measure '{measure.MeasureKey}'", Expression: measure.ExpressionText, Target: ModelAreas.Measure(measure.MeasureKey)))
            .Concat(Attributions
                .Where(attribution => attribution.Expression is not null)
                .Select(attribution => (Subject: $"The attribution of '{attribution.MetricKey}'", Expression: attribution.Expression!, Target: ModelAreas.Attribution(attribution.MetricKey))));
}

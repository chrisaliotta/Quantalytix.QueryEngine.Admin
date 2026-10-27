using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>A model's content as the rows of its tables, which a commit compares and writes.</summary>
/// <param name="Model">The model row.</param>
/// <param name="Variables">The variable rows.</param>
/// <param name="Measures">The calculated measure rows.</param>
/// <param name="Parameters">The parameter rows.</param>
/// <param name="Attributions">The attribution rows.</param>
/// <param name="Drivers">The attribution driver rows.</param>
public sealed record ModelTableRows(
    CalculationModelRecord Model,
    IReadOnlyList<VariableRecord> Variables,
    IReadOnlyList<CalculatedMeasureRecord> Measures,
    IReadOnlyList<ModelParameterRecord> Parameters,
    IReadOnlyList<MetricAttributionRecord> Attributions,
    IReadOnlyList<MetricAttributionDriverRecord> Drivers)
{
    /// <summary>The rows of a model's content.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The rows.</returns>
    public static ModelTableRows Of(ModelGraph content)
    {
        var modelId = content.Model.CalculationModelId;
        return new ModelTableRows(
            content.Model,
            content.Variables,
            content.CalculatedMeasures,
            [.. content.Parameters.Select(parameter => ModelParameterService.ToRecord(modelId, parameter))],
            [.. content.Attributions.Select(attribution => MetricAttributionService.ToRecord(modelId, attribution))],
            [.. content.Attributions.SelectMany(attribution => MetricAttributionService.DriverRecords(modelId, attribution))]);
    }

    /// <summary>Whether two models' tables hold the same rows, in any order.</summary>
    /// <param name="other">The other model's rows.</param>
    /// <returns>True when nothing would be written.</returns>
    public bool SameAs(ModelTableRows other)
    {
        return Model == other.Model
            && Same(Variables, other.Variables)
            && Same(Measures, other.Measures)
            && Same(Parameters, other.Parameters)
            && Same(Attributions, other.Attributions)
            && Same(Drivers, other.Drivers);
    }

    /// <summary>Whether two row lists hold the same rows, in any order.</summary>
    private static bool Same<TRecord>(IReadOnlyList<TRecord> before, IReadOnlyList<TRecord> after)
    {
        return before.Count == after.Count && before.ToHashSet().SetEquals(after);
    }
}

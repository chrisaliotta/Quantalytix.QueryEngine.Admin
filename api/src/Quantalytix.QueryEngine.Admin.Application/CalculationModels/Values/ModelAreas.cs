using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The areas of the model editor a model violation takes the user to, each narrowed to the item by key.</summary>
public static class ModelAreas
{
    /// <summary>The calculated measures.</summary>
    public const string Measures = "measures";

    /// <summary>The attributions of the metrics.</summary>
    public const string Attributions = "attributions";

    /// <summary>The target of a calculated measure.</summary>
    /// <param name="measureKey">The measure's key.</param>
    /// <returns>The target.</returns>
    public static ViolationTarget Measure(string measureKey)
    {
        return new ViolationTarget { Area = Measures, ItemKey = measureKey };
    }

    /// <summary>The target of a metric's attribution.</summary>
    /// <param name="metricKey">The metric's key.</param>
    /// <returns>The target.</returns>
    public static ViolationTarget Attribution(string metricKey)
    {
        return new ViolationTarget { Area = Attributions, ItemKey = metricKey };
    }
}

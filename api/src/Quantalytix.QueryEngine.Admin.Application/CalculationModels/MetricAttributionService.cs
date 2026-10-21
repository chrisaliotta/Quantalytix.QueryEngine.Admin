using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// The attributions of calculation models' metrics, each with its drivers, which a write replaces or deletes as a whole. Attributions
/// are live like the metrics they explain, so <see cref="ModelChanges"/> checks every write on the model it would leave.
/// </summary>
public sealed class MetricAttributionService(
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers)
{
    /// <summary>Lists a model's attributions, each with its drivers.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The attributions by metric key, their drivers by sort order.</returns>
    public async Task<IReadOnlyList<MetricAttribution>> ListAsync(long modelId, CancellationToken cancellationToken)
    {
        var owner = new { ModelId = modelId };
        var driversOf = (await drivers.ListAsync(owner, cancellationToken)).ToLookup(driver => driver.MetricKey, StringComparer.Ordinal);
        return
        [
            .. (await attributions.ListAsync(owner, cancellationToken))
                .OrderBy(attribution => attribution.MetricKey, StringComparer.Ordinal)
                .Select(attribution => new MetricAttribution
                {
                    MetricKey = attribution.MetricKey,
                    ExpressionText = attribution.ExpressionText,
                    WeightVariableKey = attribution.WeightVariableKey,
                    Drivers = Ordered(driversOf[attribution.MetricKey].Select(driver => new AttributionDriver
                    {
                        DriverKey = driver.DriverKey,
                        DisplayName = driver.DisplayName,
                        SortOrder = driver.SortOrder,
                        InputKeys = JsonSerializer.Deserialize<string[]>(driver.InputKeysJson)!
                    }))
                })
        ];
    }

    /// <summary>The attribution an input describes for a metric.</summary>
    /// <param name="metricKey">The metric attributed.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The attribution to store.</returns>
    public static MetricAttribution Compose(string metricKey, MetricAttributionInput input)
    {
        return new MetricAttribution
        {
            MetricKey = metricKey,
            ExpressionText = input.ExpressionText,
            WeightVariableKey = input.WeightVariableKey,
            Drivers = Ordered(input.Drivers)
        };
    }

    /// <summary>The model with an attribution added, or in place of the metric's stored one.</summary>
    /// <param name="model">The model as it stands.</param>
    /// <param name="attribution">The composed attribution.</param>
    /// <returns>The model as the write leaves it.</returns>
    public static ModelGraph Put(ModelGraph model, MetricAttribution attribution)
    {
        return model with { Attributions = [.. Without(model, attribution.MetricKey).Attributions, attribution] };
    }

    /// <summary>The model without a metric's attribution.</summary>
    /// <param name="model">The model as it stands.</param>
    /// <param name="metricKey">The metric.</param>
    /// <returns>The model as the delete leaves it.</returns>
    public static ModelGraph Without(ModelGraph model, string metricKey)
    {
        return model with { Attributions = [.. model.Attributions.Where(attribution => attribution.MetricKey != metricKey)] };
    }

    /// <summary>Stores a metric's attribution with its drivers in place of the ones stored.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="attribution">An attribution the model gate let through.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the attribution is stored.</returns>
    public async Task ReplaceAsync(long modelId, MetricAttribution attribution, CancellationToken cancellationToken)
    {
        await DeleteAsync(modelId, attribution.MetricKey, cancellationToken);
        await attributions.InsertAsync(new MetricAttributionRecord
        {
            ModelId = modelId,
            MetricKey = attribution.MetricKey,
            ExpressionText = attribution.ExpressionText,
            WeightVariableKey = attribution.WeightVariableKey
        }, cancellationToken);
        await drivers.InsertManyAsync(
        [
            .. attribution.Drivers.Select(driver => new MetricAttributionDriverRecord
            {
                ModelId = modelId,
                MetricKey = attribution.MetricKey,
                DriverKey = driver.DriverKey,
                DisplayName = driver.DisplayName,
                SortOrder = driver.SortOrder,
                InputKeysJson = JsonSerializer.Serialize(driver.InputKeys)
            })
        ], cancellationToken);
    }

    /// <summary>Deletes a metric's attribution with its drivers; the metric then has the default drivers, and no member contributions.</summary>
    /// <param name="modelId">The calculation model.</param>
    /// <param name="metricKey">The metric.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the attribution is gone.</returns>
    public async Task DeleteAsync(long modelId, string metricKey, CancellationToken cancellationToken)
    {
        var metric = new { ModelId = modelId, MetricKey = metricKey };
        await drivers.DeleteAsync(metric, cancellationToken);
        await attributions.DeleteAsync(metric, cancellationToken);
    }

    /// <summary>Drivers by sort order, then key.</summary>
    private static AttributionDriver[] Ordered(IEnumerable<AttributionDriver> unordered)
    {
        return [.. unordered.OrderBy(driver => driver.SortOrder).ThenBy(driver => driver.DriverKey, StringComparer.Ordinal)];
    }
}

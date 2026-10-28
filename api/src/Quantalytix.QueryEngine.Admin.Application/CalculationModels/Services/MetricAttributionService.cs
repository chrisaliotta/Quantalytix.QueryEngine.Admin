using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Reads the attributions of calculation models' metrics, each with its drivers in sort order, and maps them to the attribution and
/// driver rows a model change set writes.
/// </summary>
/// <param name="attributions">The attribution table.</param>
/// <param name="drivers">The driver table.</param>
public sealed class MetricAttributionService(
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers)
{
    /// <summary>Lists a model's attributions, each with its drivers.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The attributions by metric key, their drivers by sort order.</returns>
    public async Task<IReadOnlyList<Attribution>> ListAsync(long calculationModelId, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelId = calculationModelId };
        var driversOf = (await drivers.ListAsync(owner, cancellationToken)).ToLookup(driver => driver.MetricKey, StringComparer.Ordinal);
        return
        [
            .. (await attributions.ListAsync(owner, cancellationToken))
                .OrderBy(attribution => attribution.MetricKey, StringComparer.Ordinal)
                .Select(attribution => new Attribution
                {
                    MetricKey = attribution.MetricKey,
                    Expression = attribution.ExpressionText,
                    WeightVariableKey = attribution.WeightVariableKey,
                    Drivers =
                    [
                        .. driversOf[attribution.MetricKey].OrderBy(driver => driver.SortOrder).Select(driver => new AttributionDriver
                        {
                            Key = driver.DriverKey,
                            DisplayName = driver.DisplayName,
                            Inputs = JsonSerializer.Deserialize<string[]>(driver.InputKeysJson)!
                        })
                    ]
                })
        ];
    }

    /// <summary>The row an attribution is stored as, without its drivers.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="attribution">The attribution.</param>
    /// <returns>The row.</returns>
    public static MetricAttributionRecord ToRecord(long calculationModelId, Attribution attribution)
    {
        return new MetricAttributionRecord
        {
            CalculationModelId = calculationModelId,
            MetricKey = attribution.MetricKey,
            ExpressionText = attribution.Expression,
            WeightVariableKey = attribution.WeightVariableKey
        };
    }

    /// <summary>The rows an attribution's drivers are stored as, each sorted by its position.</summary>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="attribution">The attribution.</param>
    /// <returns>The driver rows.</returns>
    public static IEnumerable<MetricAttributionDriverRecord> DriverRecords(long calculationModelId, Attribution attribution)
    {
        return attribution.Drivers.Select((driver, index) => new MetricAttributionDriverRecord
        {
            CalculationModelId = calculationModelId,
            MetricKey = attribution.MetricKey,
            DriverKey = driver.Key,
            DisplayName = driver.DisplayName,
            SortOrder = index + 1,
            InputKeysJson = JsonSerializer.Serialize(driver.Inputs)
        });
    }
}

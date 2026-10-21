using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Content;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Answers whether a metric key is free in the route's model version. Variables and calculated measures share one key namespace,
/// because a formula names either through the same <c>[m.KEY]</c> reference; an edit may keep its own row's key.
/// </summary>
public sealed class ModelKeys(
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ContentKeys keys)
{
    /// <summary>Whether no other variable or calculated measure of the version has the key.</summary>
    /// <param name="key">The metric key.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>True when the key is free.</returns>
    public async Task<bool> MetricKeyFreeAsync(string key, CancellationToken cancellationToken)
    {
        var sameVariables = await variables.ListAsync(new { ModelVersionId = keys.VersionId, VariableKey = key }, cancellationToken);
        var sameMeasures = await calculatedMeasures.ListAsync(new { ModelVersionId = keys.VersionId, MeasureKey = key }, cancellationToken);
        return sameVariables.All(variable => variable.VariableId == keys.ItemId) && sameMeasures.All(measure => measure.CalculatedMeasureId == keys.ItemId);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Copies a model version's metrics into a new draft: its variables and calculated measures, each with a new ID. They reference
/// each other by key, so they copy as they are.
/// </summary>
public sealed class ModelVersionCloner(
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    IIdGenerator ids)
{
    /// <summary>Copies the source version's metrics into the target draft.</summary>
    /// <param name="sourceVersionId">The model version to copy.</param>
    /// <param name="targetVersionId">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and inserts.</param>
    /// <returns>A task that completes when the metrics are copied.</returns>
    public async Task CloneAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        var owner = new { ModelVersionId = sourceVersionId };
        await variables.InsertManyAsync(
            [.. (await variables.ListAsync(owner, cancellationToken)).Select(row => row with { VariableId = ids.NextId(), ModelVersionId = targetVersionId })],
            cancellationToken);
        await calculatedMeasures.InsertManyAsync(
            [
                .. (await calculatedMeasures.ListAsync(owner, cancellationToken))
                    .Select(row => row with { CalculatedMeasureId = ids.NextId(), ModelVersionId = targetVersionId })
            ],
            cancellationToken);
    }
}

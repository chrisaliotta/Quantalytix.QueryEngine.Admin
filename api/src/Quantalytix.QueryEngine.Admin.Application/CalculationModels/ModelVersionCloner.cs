using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Copies a model version's content into a new draft: its variables, sources, profiles with their picks, calculated measures,
/// calculation dimensions and routes. Every row gets a new ID and every reference between the copies follows it; references
/// that leave the version name datasets and canonical dimensions, which every version shares.
/// </summary>
public sealed class ModelVersionCloner(
    ITableGateway<VariableRecord> variables,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures,
    ITableGateway<CalculationDimensionRecord> dimensions,
    ITableGateway<CalculationRouteRecord> routes,
    IIdGenerator ids)
{
    /// <summary>Copies the source version's content into the target draft.</summary>
    /// <param name="sourceVersionId">The model version to copy.</param>
    /// <param name="targetVersionId">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and inserts.</param>
    /// <returns>A task that completes when the content is copied.</returns>
    public async Task CloneAsync(long sourceVersionId, long targetVersionId, CancellationToken cancellationToken)
    {
        var owner = new { ModelVersionId = sourceVersionId };
        var remap = new IdRemap(ids);
        var sourceProfiles = await profiles.ListAsync(owner, cancellationToken);
        await variables.InsertManyAsync(
            [.. (await variables.ListAsync(owner, cancellationToken)).Select(row => row with { VariableId = remap.Map(row.VariableId), ModelVersionId = targetVersionId })],
            cancellationToken);
        await sources.InsertManyAsync(
            [
                .. (await sources.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    VariableSourceId = remap.Map(row.VariableSourceId), ModelVersionId = targetVersionId, VariableId = remap.Map(row.VariableId)
                })
            ],
            cancellationToken);
        await profiles.InsertManyAsync(
            [.. sourceProfiles.Select(row => row with { SourceProfileId = remap.Map(row.SourceProfileId), ModelVersionId = targetVersionId })],
            cancellationToken);
        await picks.InsertManyAsync(
            [
                .. (await picks.ListAsync(new { SourceProfileId = sourceProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken))
                    .Select(row => new SourceProfileVariableRecord
                    {
                        SourceProfileId = remap.Map(row.SourceProfileId), VariableId = remap.Map(row.VariableId), VariableSourceId = remap.Map(row.VariableSourceId)
                    })
            ],
            cancellationToken);
        await calculatedMeasures.InsertManyAsync(
            [
                .. (await calculatedMeasures.ListAsync(owner, cancellationToken))
                    .Select(row => row with { CalculatedMeasureId = remap.Map(row.CalculatedMeasureId), ModelVersionId = targetVersionId })
            ],
            cancellationToken);
        await dimensions.InsertManyAsync(
            [
                .. (await dimensions.ListAsync(owner, cancellationToken))
                    .Select(row => row with { CalculationDimensionId = remap.Map(row.CalculationDimensionId), ModelVersionId = targetVersionId })
            ],
            cancellationToken);
        await routes.InsertManyAsync(
            [
                .. (await routes.ListAsync(owner, cancellationToken)).Select(row => row with
                {
                    CalculationBindingId = remap.Map(row.CalculationBindingId),
                    ModelVersionId = targetVersionId,
                    CalculationDimensionId = remap.Map(row.CalculationDimensionId),
                    VariableSourceId = remap.MapOptional(row.VariableSourceId)
                })
            ],
            cancellationToken);
    }
}

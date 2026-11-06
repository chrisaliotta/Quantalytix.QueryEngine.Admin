using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Copies a calculation model version, its settings and content included, into a snapshot, for capture and restore. Variables and
/// calculated measures get new ids; parameters, attributions and drivers key by the version and their keys.
/// </summary>
/// <param name="versions">Reads the source and writes the copy's row.</param>
/// <param name="variables">The variable table.</param>
/// <param name="measures">The calculated measure table.</param>
/// <param name="parameters">The parameter table.</param>
/// <param name="attributions">The attribution table.</param>
/// <param name="drivers">The attribution driver table.</param>
/// <param name="remover">Clears the content a copy replaces.</param>
/// <param name="ids">Allocates the copies' ids.</param>
/// <param name="time">Stamps the copy.</param>
public sealed class CalculationModelVersionCopier(
    ITableGateway<CalculationModelVersionRecord> versions,
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> measures,
    ITableGateway<ModelParameterRecord> parameters,
    ITableGateway<MetricAttributionRecord> attributions,
    ITableGateway<MetricAttributionDriverRecord> drivers,
    CalculationModelVersionRemover remover,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>calculation_model</c>.</summary>
    public string Kind => SnapshotKinds.CalculationModel;

    /// <summary>Copies versions into a snapshot, each as a new version or over another version of its model.</summary>
    /// <param name="copies">The versions to copy, each with the version whose settings and content it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var now = time.GetUtcNow().UtcDateTime;
        var owner = new { CalculationModelVersionId = batch.SourceIds };
        var sources = await versions.ListAsync(owner, cancellationToken);
        await remover.RemoveContentAsync(batch.ReplacedIds, cancellationToken);
        await versions.InsertManyAsync(
            [.. sources.Select(source => source with { CalculationModelVersionId = batch.Targets[source.CalculationModelVersionId], SnapshotId = targetSnapshotId, CreatedAt = now })],
            cancellationToken);

        var targets = batch.Targets;
        await variables.InsertManyAsync(
            [.. (await variables.ListAsync(owner, cancellationToken)).Select(row => row with { VariableId = ids.NextId(), CalculationModelVersionId = targets[row.CalculationModelVersionId] })],
            cancellationToken);
        await measures.InsertManyAsync(
            [.. (await measures.ListAsync(owner, cancellationToken)).Select(row => row with { CalculatedMeasureId = ids.NextId(), CalculationModelVersionId = targets[row.CalculationModelVersionId] })],
            cancellationToken);
        await parameters.InsertManyAsync(
            [.. (await parameters.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = targets[row.CalculationModelVersionId] })],
            cancellationToken);
        await attributions.InsertManyAsync(
            [.. (await attributions.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = targets[row.CalculationModelVersionId] })],
            cancellationToken);
        await drivers.InsertManyAsync(
            [.. (await drivers.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = targets[row.CalculationModelVersionId] })],
            cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>The version's settings and content: variables, calculated measures, parameters, attributions and drivers, all by key.</summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var owner = new { CalculationModelVersionId = versionId };
        var version = await versions.GetAsync(owner, cancellationToken);
        return ContentKey.Of(
            [version with { CalculationModelVersionId = 0, SnapshotId = 0, CreatedAt = default }],
            (await variables.ListAsync(owner, cancellationToken)).Select(row => row with { VariableId = 0, CalculationModelVersionId = 0 }),
            (await measures.ListAsync(owner, cancellationToken)).Select(row => row with { CalculatedMeasureId = 0, CalculationModelVersionId = 0 }),
            (await parameters.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = 0 }),
            (await attributions.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = 0 }),
            (await drivers.ListAsync(owner, cancellationToken)).Select(row => row with { CalculationModelVersionId = 0 }));
    }
}

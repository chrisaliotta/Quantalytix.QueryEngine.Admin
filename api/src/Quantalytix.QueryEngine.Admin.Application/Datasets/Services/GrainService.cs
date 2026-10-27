using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Lists, creates, edits and deletes the grains of dataset versions, each with its ordered component fields.</summary>
public sealed class GrainService(
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    ITableGateway<MeasureRecord> measures,
    IIdGenerator ids)
{
    /// <summary>Lists the grains of a dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The grains sorted by key, each with its fields in ordinal order.</returns>
    public async Task<IReadOnlyList<GrainDetail>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionGrains = await grains.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var components = await grainFields.ListAsync(new { DatasetGrainId = versionGrains.Select(grain => grain.DatasetGrainId).ToArray() }, cancellationToken);
        var componentsByGrain = components.ToLookup(component => component.DatasetGrainId);
        return
        [
            .. versionGrains
                .OrderBy(grain => grain.GrainKey, StringComparer.OrdinalIgnoreCase)
                .Select(grain => Detail(grain, [.. componentsByGrain[grain.DatasetGrainId].OrderBy(component => component.Ordinal).Select(component => component.FieldId)]))
        ];
    }

    /// <summary>Creates a grain in a draft dataset version: the grain first, then its fields with ordinals 1 to n.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new grain.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The new grain.</returns>
    public async Task<GrainDetail> CreateAsync(long versionId, GrainInput input, CancellationToken cancellationToken)
    {
        var grain = new DatasetGrainRecord
        {
            DatasetGrainId = ids.NextId(),
            DatasetVersionId = versionId,
            GrainKey = input.GrainKey,
            GrainKind = input.GrainKind
        };
        await grains.InsertAsync(grain, cancellationToken);
        await grainFields.InsertManyAsync(Components(grain.DatasetGrainId, input.FieldIds), cancellationToken);
        return Detail(grain, input.FieldIds);
    }

    /// <summary>Changes a grain's kind and replaces its fields; the key stays.</summary>
    /// <param name="reference">The grain and its draft version.</param>
    /// <param name="input">The new kind and fields.</param>
    /// <param name="cancellationToken">Cancels the read and the writes.</param>
    /// <returns>The updated grain.</returns>
    public async Task<GrainDetail> UpdateAsync(GrainReference reference, GrainUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await grains.GetAsync(new { reference.DatasetGrainId }, cancellationToken);
        var grain = current with { GrainKind = input.GrainKind };
        await grains.UpdateAsync(grain, cancellationToken);
        await grainFields.DeleteAsync(new { reference.DatasetGrainId }, cancellationToken);
        await grainFields.InsertManyAsync(Components(grain.DatasetGrainId, input.FieldIds), cancellationToken);
        return Detail(grain, input.FieldIds);
    }

    /// <summary>Deletes a grain that no measure time policy selects observations by: its fields first, then the grain.</summary>
    /// <param name="reference">The grain and its draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the grain was deleted; otherwise the detail naming the measures whose time policies use it.</returns>
    public async Task<string?> DeleteAsync(GrainReference reference, CancellationToken cancellationToken)
    {
        var grain = await grains.GetAsync(new { reference.DatasetGrainId }, cancellationToken);
        var policies = await timePolicies.ListAsync(new { SelectionDatasetGrainId = reference.DatasetGrainId }, cancellationToken);
        var selecting = await measures.ListAsync(new { MeasureId = policies.Select(policy => policy.MeasureId).ToArray() }, cancellationToken);
        var blockers = new DeleteBlockers($"Grain '{grain.GrainKey}'");
        blockers.AddNamed("referenced_by_measure", "it is the selection grain of the time policies of measures", selecting.Select(measure => $"'{measure.MeasureKey}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await grainFields.DeleteAsync(new { reference.DatasetGrainId }, cancellationToken);
        await grains.DeleteAsync(new { reference.DatasetGrainId }, cancellationToken);
        return null;
    }

    /// <summary>The component rows of a grain, numbered from 1 in list order.</summary>
    private static DatasetGrainFieldRecord[] Components(long datasetGrainId, IReadOnlyList<long> fieldIds)
    {
        return [.. fieldIds.Select((fieldId, index) => new DatasetGrainFieldRecord { DatasetGrainId = datasetGrainId, Ordinal = index + 1, FieldId = fieldId })];
    }

    /// <summary>A grain with its fields in ordinal order.</summary>
    private static GrainDetail Detail(DatasetGrainRecord grain, IReadOnlyList<long> fieldIds)
    {
        return new GrainDetail { DatasetGrainId = grain.DatasetGrainId, GrainKey = grain.GrainKey, GrainKind = grain.GrainKind, FieldIds = fieldIds };
    }
}

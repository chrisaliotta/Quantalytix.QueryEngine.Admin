using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Copies a dataset version, its settings and enabled flag included, into a snapshot. Every ID inside the version is remapped through
/// one <see cref="IdRemap"/>, so references between the copied rows point at the copies; references to identities (dimensions, roles,
/// mapping sets and bridges) keep their IDs.
/// </summary>
public sealed class DatasetVersionCopier(
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content,
    IIdGenerator ids,
    TimeProvider time) : IVersionCopier
{
    /// <summary><c>dataset</c>.</summary>
    public string Kind => SnapshotKinds.Dataset;

    /// <summary>Copies versions' settings and content into a snapshot, each as a new version or over another version of its dataset.</summary>
    /// <param name="copies">The versions to copy, each with the version whose content it replaces, if any.</param>
    /// <param name="targetSnapshotId">The snapshot the copies belong to.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copies' ids.</returns>
    public async Task<IReadOnlyList<long>> CopyAsync(IReadOnlyList<VersionCopy> copies, long targetSnapshotId, CancellationToken cancellationToken)
    {
        var batch = new VersionCopyBatch(copies, ids);
        var remap = new IdRemap(ids);
        var now = time.GetUtcNow().UtcDateTime;
        var sources = await versions.ListAsync(new { DatasetVersionId = batch.SourceIds }, cancellationToken);
        var sourceContent = await content.LoadAsync(batch.SourceIds, cancellationToken);
        await content.DeleteAsync(batch.ReplacedIds, cancellationToken);
        await versions.InsertManyAsync(
            [
                .. sources.Select(source => source with
                {
                    DatasetVersionId = batch.Targets[source.DatasetVersionId],
                    SnapshotId = targetSnapshotId,
                    DefaultTimeBindingId = remap.MapOptional(source.DefaultTimeBindingId),
                    CreatedAt = now
                })
            ],
            cancellationToken);
        await content.InsertAsync(Remap(sourceContent, batch.Targets, remap), cancellationToken);
        return batch.CopyIds;
    }

    /// <summary>
    /// The version's settings and content: fields, grains, measures by key; bindings by their role, or a time binding by its source
    /// field's key; and every reference between them by the key it names.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The content key.</returns>
    public async Task<string> ContentKeyAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var rows = await content.LoadAsync([versionId], cancellationToken);
        var fieldKeys = rows.Fields.ToDictionary(field => field.FieldId, field => field.FieldKey);
        var grainKeys = rows.Grains.ToDictionary(grain => grain.DatasetGrainId, grain => grain.GrainKey);
        var measureKeys = rows.Measures.ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var bindingKeys = rows.Bindings.ToDictionary(
            binding => binding.DimensionBindingId,
            binding => binding.DimensionRoleId is { } roleId ? $"role:{roleId}" : $"field:{fieldKeys[binding.SourceFieldId!.Value]}");
        return ContentKey.Of(
            [new { Version = version with { DatasetVersionId = 0, SnapshotId = 0, DefaultTimeBindingId = null, CreatedAt = default }, DefaultTimeBinding = ContentKey.KeyOf(bindingKeys, version.DefaultTimeBindingId) }],
            rows.Fields.Select(field => field with { FieldId = 0, DatasetVersionId = 0 }),
            rows.Grains.Select(grain => grain with { DatasetGrainId = 0, DatasetVersionId = 0 }),
            rows.GrainFields.Select(component => new { component.Ordinal, Grain = grainKeys[component.DatasetGrainId], Field = fieldKeys[component.FieldId] }),
            rows.Bindings.Select(binding => new
            {
                Binding = binding with { DimensionBindingId = 0, DatasetVersionId = 0, SourceFieldId = null, SourceDimensionBindingId = null },
                Key = bindingKeys[binding.DimensionBindingId],
                SourceField = ContentKey.KeyOf(fieldKeys, binding.SourceFieldId),
                SourceBinding = ContentKey.KeyOf(bindingKeys, binding.SourceDimensionBindingId)
            }),
            rows.TimeBindings.Select(timeBinding => new
            {
                TimeBinding = timeBinding with { DimensionBindingId = 0, IntervalEndFieldId = null },
                Binding = bindingKeys[timeBinding.DimensionBindingId],
                IntervalEndField = ContentKey.KeyOf(fieldKeys, timeBinding.IntervalEndFieldId)
            }),
            rows.Measures.Select(measure => measure with { MeasureId = 0, DatasetVersionId = 0 }),
            rows.Components.Select(component => new { component.ComponentRole, Measure = measureKeys[component.MeasureId], Field = fieldKeys[component.FieldId] }),
            rows.Dependencies.Select(dependency => new { Measure = measureKeys[dependency.MeasureId], DependsOn = measureKeys[dependency.DependencyMeasureId] }),
            rows.TimePolicies.Select(policy => new
            {
                Policy = policy with { MeasureId = 0, RequiredTimeBindingId = null, SelectionDatasetGrainId = null },
                Measure = measureKeys[policy.MeasureId],
                RequiredTimeBinding = ContentKey.KeyOf(bindingKeys, policy.RequiredTimeBindingId),
                SelectionGrain = ContentKey.KeyOf(grainKeys, policy.SelectionDatasetGrainId)
            }));
    }

    /// <summary>The content rows of the copies: new IDs, each row's copied version, and internal references remapped.</summary>
    private static DatasetVersionContent Remap(DatasetVersionContent source, IReadOnlyDictionary<long, long> targets, IdRemap remap)
    {
        return new DatasetVersionContent
        {
            Fields = [.. source.Fields.Select(field => field with { FieldId = remap.Map(field.FieldId), DatasetVersionId = targets[field.DatasetVersionId] })],
            Grains = [.. source.Grains.Select(grain => grain with { DatasetGrainId = remap.Map(grain.DatasetGrainId), DatasetVersionId = targets[grain.DatasetVersionId] })],
            GrainFields =
            [
                .. source.GrainFields.Select(component => component with
                {
                    DatasetGrainId = remap.Map(component.DatasetGrainId),
                    FieldId = remap.Map(component.FieldId)
                })
            ],
            Bindings =
            [
                .. source.Bindings.Select(binding => binding with
                {
                    DimensionBindingId = remap.Map(binding.DimensionBindingId),
                    DatasetVersionId = targets[binding.DatasetVersionId],
                    SourceFieldId = remap.MapOptional(binding.SourceFieldId),
                    SourceDimensionBindingId = remap.MapOptional(binding.SourceDimensionBindingId)
                })
            ],
            TimeBindings =
            [
                .. source.TimeBindings.Select(timeBinding => timeBinding with
                {
                    DimensionBindingId = remap.Map(timeBinding.DimensionBindingId),
                    IntervalEndFieldId = remap.MapOptional(timeBinding.IntervalEndFieldId)
                })
            ],
            Measures = [.. source.Measures.Select(measure => measure with { MeasureId = remap.Map(measure.MeasureId), DatasetVersionId = targets[measure.DatasetVersionId] })],
            Components =
            [
                .. source.Components.Select(component => component with
                {
                    MeasureId = remap.Map(component.MeasureId),
                    FieldId = remap.Map(component.FieldId)
                })
            ],
            Dependencies =
            [
                .. source.Dependencies.Select(dependency => dependency with
                {
                    MeasureId = remap.Map(dependency.MeasureId),
                    DependencyMeasureId = remap.Map(dependency.DependencyMeasureId)
                })
            ],
            TimePolicies =
            [
                .. source.TimePolicies.Select(policy => policy with
                {
                    MeasureId = remap.Map(policy.MeasureId),
                    RequiredTimeBindingId = remap.MapOptional(policy.RequiredTimeBindingId),
                    SelectionDatasetGrainId = remap.MapOptional(policy.SelectionDatasetGrainId)
                })
            ]
        };
    }
}

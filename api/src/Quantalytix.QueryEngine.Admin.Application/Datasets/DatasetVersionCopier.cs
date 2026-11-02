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

    /// <summary>Copies a version's settings and content into a snapshot, as a new version or over another version of the dataset.</summary>
    /// <param name="versionId">The version to copy.</param>
    /// <param name="targetSnapshotId">The snapshot the copy belongs to.</param>
    /// <param name="targetVersionId">The version whose content the copy replaces, or null for a new version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The copy's id.</returns>
    public async Task<long> CopyAsync(long versionId, long targetSnapshotId, long? targetVersionId, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var source = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var sourceContent = await content.LoadAsync([versionId], cancellationToken);
        var copy = source with
        {
            DatasetVersionId = targetVersionId ?? ids.NextId(),
            SnapshotId = targetSnapshotId,
            DefaultTimeBindingId = remap.MapOptional(source.DefaultTimeBindingId),
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        if (targetVersionId is { } target)
        {
            await content.DeleteAsync([target], cancellationToken);
            await versions.UpdateAsync(copy, cancellationToken);
        }
        else
        {
            await versions.InsertAsync(copy, cancellationToken);
        }

        await content.InsertAsync(Remap(sourceContent, copy.DatasetVersionId, remap), cancellationToken);
        return copy.DatasetVersionId;
    }

    /// <summary>The content rows of the copy: new IDs, the new version, and internal references remapped.</summary>
    private static DatasetVersionContent Remap(DatasetVersionContent source, long versionId, IdRemap remap)
    {
        return new DatasetVersionContent
        {
            Fields = [.. source.Fields.Select(field => field with { FieldId = remap.Map(field.FieldId), DatasetVersionId = versionId })],
            Grains = [.. source.Grains.Select(grain => grain with { DatasetGrainId = remap.Map(grain.DatasetGrainId), DatasetVersionId = versionId })],
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
                    DatasetVersionId = versionId,
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
            Measures = [.. source.Measures.Select(measure => measure with { MeasureId = remap.Map(measure.MeasureId), DatasetVersionId = versionId })],
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

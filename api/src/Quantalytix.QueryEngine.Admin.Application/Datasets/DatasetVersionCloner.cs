using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Copies a dataset version into a new draft. Every ID inside the version is remapped through one <see cref="IdRemap"/>,
/// so references between the copied rows point at the copies; references to other aggregates (dimensions, their versions,
/// levels and members, mapping sets and bridge versions) keep their IDs.
/// </summary>
public sealed class DatasetVersionCloner(
    ITableGateway<DatasetVersionRecord> versions,
    DatasetVersionContentStore content,
    IIdGenerator ids)
{
    /// <summary>Creates a draft copy of a version's settings and content.</summary>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="datasetId">The dataset that owns the new draft.</param>
    /// <param name="versionNumber">The new draft's version number.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft version.</returns>
    public async Task<DatasetVersionRecord> CloneAsync(long sourceVersionId, long datasetId, int versionNumber, CancellationToken cancellationToken)
    {
        var remap = new IdRemap(ids);
        var source = await versions.GetAsync(new { DatasetVersionId = sourceVersionId }, cancellationToken);
        var sourceContent = await content.LoadAsync([sourceVersionId], cancellationToken);
        var clone = source with
        {
            DatasetVersionId = remap.Map(source.DatasetVersionId),
            DatasetId = datasetId,
            VersionNumber = versionNumber,
            Status = PublicationStatuses.Draft,
            DefaultTimeBindingId = remap.MapOptional(source.DefaultTimeBindingId),
            PublishedAt = null
        };
        await versions.InsertAsync(clone, cancellationToken);
        await content.InsertAsync(Remap(sourceContent, clone.DatasetVersionId, remap), cancellationToken);
        return clone;
    }

    /// <summary>The content rows of the clone: new IDs, the new version, and internal references remapped.</summary>
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

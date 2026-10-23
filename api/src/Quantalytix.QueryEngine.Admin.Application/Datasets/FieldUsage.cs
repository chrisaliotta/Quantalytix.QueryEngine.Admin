using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Finds the rows that reference a field by ID and so keep it from being deleted: grain components, bindings that read the
/// field, time bindings whose intervals end at it, and measure components.
/// </summary>
public sealed class FieldUsage(
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DatasetGrainFieldRecord> grainFields,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureComponentRecord> components,
    BindingNames names)
{
    /// <summary>Describes every row that references a field.</summary>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One description per use, such as <c>grain 'rowIdentity'</c>; empty when nothing references the field.</returns>
    public async Task<IReadOnlyList<string>> ListAsync(long fieldId, CancellationToken cancellationToken)
    {
        var grainComponents = await grainFields.ListAsync(new { FieldId = fieldId }, cancellationToken);
        var usingGrains = await grains.ListAsync(new { DatasetGrainId = grainComponents.Select(row => row.DatasetGrainId).Distinct().ToArray() }, cancellationToken);
        var readingBindings = await bindings.ListAsync(new { SourceFieldId = fieldId }, cancellationToken);
        var endingTimeBindings = await timeBindings.ListAsync(new { IntervalEndFieldId = fieldId }, cancellationToken);
        var endingBindings = await bindings.ListAsync(new { DimensionBindingId = endingTimeBindings.Select(row => row.DimensionBindingId).ToArray() }, cancellationToken);
        var measureComponents = await components.ListAsync(new { FieldId = fieldId }, cancellationToken);
        var componentMeasures = await measures.ListAsync(new { MeasureId = measureComponents.Select(row => row.MeasureId).Distinct().ToArray() }, cancellationToken);
        var measureKeys = componentMeasures.ToDictionary(measure => measure.MeasureId, measure => measure.MeasureKey);
        var named = await names.OfAsync([.. readingBindings, .. endingBindings], cancellationToken);
        return
        [
            .. usingGrains.Select(grain => $"grain '{grain.GrainKey}'"),
            .. readingBindings.Select(binding => $"binding '{named[binding.DimensionBindingId]}'"),
            .. endingBindings.Select(binding => $"the interval end of time binding '{named[binding.DimensionBindingId]}'"),
            .. measureComponents.Select(component => $"the {component.ComponentRole} component of measure '{measureKeys[component.MeasureId]}'")
        ];
    }
}

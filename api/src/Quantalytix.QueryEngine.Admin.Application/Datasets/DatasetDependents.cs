using Quantalytix.QueryEngine.Admin.Application.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The delete-blocker graph of a dataset version (OI-35): what reads each item. A reference by id (a grain component, a binding's
/// source field, a time policy's grain) blocks the item's delete; a reference by key inside an expression or formula does not, and
/// the publication rules report it once the item is gone.
/// </summary>
public static class DatasetDependents
{
    /// <summary>The dependents of every item, as the version read model carries them.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="bindingNames">Each binding's name, by id.</param>
    /// <param name="presentId">Writes each id.</param>
    /// <returns>Each read item's dependents, by the item's id.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<DatasetDependent>> Of(
        DatasetSnapshot snapshot,
        IReadOnlyDictionary<long, string> bindingNames,
        Func<long, string> presentId)
    {
        return References(snapshot, bindingNames)
            .GroupBy(reference => reference.TargetId)
            .ToDictionary(
                group => presentId(group.Key),
                group => (IReadOnlyList<DatasetDependent>)[.. group.Select(reference => reference.Dependent(presentId))]);
    }

    /// <summary>The references that keep an item from being deleted.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="itemId">The field, grain, binding or measure.</param>
    /// <returns>The blocking references.</returns>
    public static IReadOnlyList<DatasetReference> Blocking(DatasetSnapshot snapshot, long itemId)
    {
        return [.. References(snapshot, Bindings.DatasetBindingNames.Of(snapshot)).Where(reference => reference.TargetId == itemId && reference.BlocksDelete)];
    }

    /// <summary>Every reference between the version's items.</summary>
    private static IEnumerable<DatasetReference> References(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> bindingNames)
    {
        var fieldIds = snapshot.Fields.ToDictionary(field => field.FieldKey, field => field.FieldId, StringComparer.Ordinal);
        var measureIds = snapshot.Measures.ToDictionary(measure => measure.MeasureKey, measure => measure.MeasureId, StringComparer.Ordinal);
        var grains = snapshot.Grains.ToDictionary(grain => grain.DatasetGrainId);
        var measures = snapshot.Measures.ToDictionary(measure => measure.MeasureId);

        foreach (var field in snapshot.Fields.Where(field => field.ExpressionText is not null))
        {
            foreach (var key in ExpressionInspector.Inspect(field.ExpressionText!).FieldKeys.Where(fieldIds.ContainsKey))
                yield return new DatasetReference(fieldIds[key], DatasetTabs.Fields, field.FieldId, field.FieldKey, "expression", false);
        }

        foreach (var component in snapshot.GrainFields)
        {
            var grain = grains[component.DatasetGrainId];
            yield return new DatasetReference(component.FieldId, DatasetTabs.Grains, grain.DatasetGrainId, grain.GrainKey, $"{grain.GrainKind} grain component", true);
        }

        foreach (var binding in snapshot.Bindings)
        {
            var name = bindingNames[binding.DimensionBindingId];
            if (binding.SourceFieldId is { } sourceFieldId)
                yield return new DatasetReference(sourceFieldId, DatasetTabs.Bindings, binding.DimensionBindingId, name, "binding source field", true);
            if (binding.SourceDimensionBindingId is { } sourceBindingId)
                yield return new DatasetReference(sourceBindingId, DatasetTabs.Bindings, binding.DimensionBindingId, name, "bridge source binding", true);
        }

        foreach (var time in snapshot.TimeBindings.Where(time => time.IntervalEndFieldId is not null))
            yield return new DatasetReference(time.IntervalEndFieldId!.Value, DatasetTabs.Bindings, time.DimensionBindingId, bindingNames[time.DimensionBindingId], "interval end field", true);

        if (snapshot.Version.DefaultTimeBindingId is { } defaultTimeBindingId)
            yield return new DatasetReference(defaultTimeBindingId, DatasetTabs.Settings, null, "Settings", "default time binding", true);

        foreach (var component in snapshot.Components)
        {
            var measure = measures[component.MeasureId];
            yield return new DatasetReference(component.FieldId, DatasetTabs.Measures, measure.MeasureId, measure.MeasureKey, $"{component.ComponentRole} component", true);
        }

        foreach (var measure in snapshot.Measures)
        {
            if (measure.RowFilterExpression is { } rowFilter)
            {
                foreach (var key in ExpressionInspector.Inspect(rowFilter).FieldKeys.Where(fieldIds.ContainsKey))
                    yield return new DatasetReference(fieldIds[key], DatasetTabs.Measures, measure.MeasureId, measure.MeasureKey, "row filter", false);
            }

            if (measure.ExpressionText is { } formula)
            {
                foreach (var key in ExpressionInspector.Inspect(formula).MeasureKeys.Where(measureIds.ContainsKey))
                    yield return new DatasetReference(measureIds[key], DatasetTabs.Measures, measure.MeasureId, measure.MeasureKey, "formula", false);
            }
        }

        foreach (var policy in snapshot.TimePolicies)
        {
            var measure = measures[policy.MeasureId];
            if (policy.RequiredTimeBindingId is { } requiredBindingId)
                yield return new DatasetReference(requiredBindingId, DatasetTabs.Measures, measure.MeasureId, measure.MeasureKey, "required time binding", true);
            if (policy.SelectionDatasetGrainId is { } grainId)
                yield return new DatasetReference(grainId, DatasetTabs.Measures, measure.MeasureId, measure.MeasureKey, "selects by series grain", true);
        }
    }
}

using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>component_fields_in_version</c>: every measure component reads a field of the measure's version.</summary>
public sealed class ComponentFieldsInVersionRule : MeasureRule
{
    /// <summary><c>component_fields_in_version</c>.</summary>
    protected override string Code => "component_fields_in_version";

    /// <summary>Reports every component that names no field of the version.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such component.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var fieldIds = snapshot.Fields.Select(field => field.FieldId).ToHashSet();
        var components = snapshot.Components.ToLookup(component => component.MeasureId);
        return ByKey(snapshot).SelectMany(measure => components[measure.MeasureId]
            .Where(component => !fieldIds.Contains(component.FieldId))
            .OrderBy(component => component.ComponentRole, StringComparer.Ordinal)
            .Select(component => Violation(
                measure,
                $"Measure '{measure.MeasureKey}' reads field {component.FieldId} as its {component.ComponentRole} component, which is not a field of this version.")));
    }
}

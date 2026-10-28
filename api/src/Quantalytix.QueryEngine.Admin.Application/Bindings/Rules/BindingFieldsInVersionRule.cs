using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary><c>binding_fields_in_version</c>: a binding's source field and a time binding's interval-end field are fields of the version.</summary>
public sealed class BindingFieldsInVersionRule : BindingRule
{
    /// <summary><c>binding_fields_in_version</c>.</summary>
    protected override string Code => "binding_fields_in_version";

    /// <summary>Reports every binding field that is not a field of the version.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per such field.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var fieldIds = snapshot.Fields.Select(field => field.FieldId).ToHashSet();
        var times = snapshot.TimeBindings.ToDictionary(time => time.DimensionBindingId);
        foreach (var binding in ByName(snapshot.Bindings, names))
        {
            var name = names[binding.DimensionBindingId];
            if (binding.SourceFieldId is { } sourceFieldId && !fieldIds.Contains(sourceFieldId))
                yield return Violation(binding, names, $"Binding '{name}' reads field {sourceFieldId}, which is not a field of this version.");
            if (times.TryGetValue(binding.DimensionBindingId, out var time) && time.IntervalEndFieldId is { } endFieldId && !fieldIds.Contains(endFieldId))
                yield return Violation(binding, names, $"Time binding '{name}' ends its intervals at field {endFieldId}, which is not a field of this version.");
        }
    }
}

using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary><c>binding_source_field_groupable</c>: a groupable binding reads a groupable source field.</summary>
public sealed class BindingSourceFieldGroupableRule : BindingRule
{
    /// <summary><c>binding_source_field_groupable</c>.</summary>
    protected override string Code => "binding_source_field_groupable";

    /// <summary>Reports every groupable binding whose source field is not groupable.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per such binding.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var fields = snapshot.Fields.ToDictionary(field => field.FieldId);
        return ByName(snapshot.Bindings, names)
            .Where(binding => binding.IsGroupable && binding.SourceFieldId is { } fieldId && fields.TryGetValue(fieldId, out var field) && !field.IsGroupable)
            .Select(binding => Violation(
                binding,
                names,
                $"Binding '{names[binding.DimensionBindingId]}' is groupable, but its source field '{fields[binding.SourceFieldId!.Value].FieldKey}' is not; " +
                "make the field groupable or the binding not groupable."));
    }
}

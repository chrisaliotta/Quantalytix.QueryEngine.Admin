using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary><c>mapped_binding_mapping_set_dimension</c>: a mapped binding maps through a mapping set of the tenant that targets its dimension.</summary>
public sealed class MappedBindingMappingSetDimensionRule : BindingRule
{
    /// <summary><c>mapped_binding_mapping_set_dimension</c>.</summary>
    protected override string Code => "mapped_binding_mapping_set_dimension";

    /// <summary>Reports every mapped binding whose mapping set is missing or targets another dimension.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per such binding.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var sets = snapshot.Surroundings.MappingSets.ToDictionary(set => set.MappingSetId);
        foreach (var binding in ByName(snapshot.Bindings.Where(binding => binding.BindingKind == BindingKinds.Mapped && binding.MappingSetId is not null), names))
        {
            var name = names[binding.DimensionBindingId];
            if (!sets.TryGetValue(binding.MappingSetId!.Value, out var set))
                yield return Violation(binding, names, $"Mapped binding '{name}' maps through mapping set {binding.MappingSetId}, which does not exist.");
            else if (set.DimensionId != binding.DimensionId)
                yield return Violation(binding, names, $"Mapped binding '{name}' maps through mapping set '{set.MappingSetKey}', which targets another dimension than the binding.");
        }
    }
}

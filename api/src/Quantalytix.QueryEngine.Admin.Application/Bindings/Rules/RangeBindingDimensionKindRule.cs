using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary><c>range_binding_dimension_kind</c>: a range binding binds a range dimension, whose bands classify the values.</summary>
public sealed class RangeBindingDimensionKindRule : BindingRule
{
    /// <summary><c>range_binding_dimension_kind</c>.</summary>
    protected override string Code => "range_binding_dimension_kind";

    /// <summary>Reports every range binding whose dimension is missing or not of the range kind.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per such binding.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var dimensions = snapshot.Surroundings.Dimensions.ToDictionary(dimension => dimension.DimensionId);
        foreach (var binding in ByName(snapshot.Bindings.Where(binding => binding.BindingKind == BindingKinds.Range), names))
        {
            var name = names[binding.DimensionBindingId];
            if (!dimensions.TryGetValue(binding.DimensionId, out var dimension))
                yield return Violation(binding, names, $"Range binding '{name}' binds dimension {binding.DimensionId}, which does not exist.");
            else if (dimension.DimensionKind != DimensionKinds.Range)
                yield return Violation(
                    binding,
                    names,
                    $"Range binding '{name}' binds '{dimension.DimensionKey}', a {dimension.DimensionKind} dimension; range bindings classify through the bands of a range dimension.");
        }
    }
}

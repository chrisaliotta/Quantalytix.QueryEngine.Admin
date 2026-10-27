using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary><c>time_binding_dimension_kind</c>: a time binding binds a time dimension and has its time axis.</summary>
public sealed class TimeBindingDimensionKindRule : BindingRule
{
    /// <summary><c>time_binding_dimension_kind</c>.</summary>
    protected override string Code => "time_binding_dimension_kind";

    /// <summary>Reports every time binding whose dimension is missing or not of the time kind, or that has no time axis.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>Up to two violations per time binding.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var dimensions = snapshot.Surroundings.Dimensions.ToDictionary(dimension => dimension.DimensionId);
        var axes = snapshot.TimeBindings.Select(time => time.DimensionBindingId).ToHashSet();
        foreach (var binding in ByName(snapshot.Bindings.Where(binding => binding.BindingKind == BindingKinds.Time), names))
        {
            var name = names[binding.DimensionBindingId];
            if (!dimensions.TryGetValue(binding.DimensionId, out var dimension))
                yield return Violation(binding, names, $"Time binding '{name}' binds dimension {binding.DimensionId}, which does not exist.");
            else if (dimension.DimensionKind != DimensionKinds.Time)
                yield return Violation(binding, names, $"Time binding '{name}' binds '{dimension.DimensionKey}', a {dimension.DimensionKind} dimension; time bindings bind a time dimension.");
            if (!axes.Contains(binding.DimensionBindingId))
                yield return Violation(binding, names, $"Time binding '{name}' has no time extension.");
        }
    }
}

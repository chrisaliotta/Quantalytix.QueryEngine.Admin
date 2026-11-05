using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// <c>compound_inputs</c>: a compound measure's rate reads no other compound measure, directly or through formulas, since the
/// engine compiles each compound
/// measure's steps from ordinary measures, and the time binding it aggregates on, its time policy's or the version's default,
/// allows its compounding periodicity, since each step is a period at that periodicity.
/// </summary>
public sealed class CompoundInputsRule : MeasureRule
{
    /// <summary><c>compound_inputs</c>.</summary>
    protected override string Code => "compound_inputs";

    /// <summary>Reports every compound input, and every compound measure whose time binding lacks its compounding periodicity.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per problem.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var measures = snapshot.Measures.ToDictionary(measure => measure.MeasureId);
        var edges = snapshot.Dependencies.ToLookup(dependency => dependency.MeasureId, dependency => dependency.DependencyMeasureId);
        var policies = snapshot.TimePolicies.ToDictionary(policy => policy.MeasureId);
        var allowed = snapshot.TimeBindings.ToDictionary(
            time => time.DimensionBindingId,
            time => JsonSerializer.Deserialize<string[]>(time.AllowedPeriodicitiesJson)!);
        foreach (var measure in ByKey(snapshot).Where(measure => measure.MeasureKind == MeasureKinds.Compound))
        {
            var compoundInputs = MeasureDependencies.Reachable(measure.MeasureId, edges)
                .Select(id => measures[id])
                .Where(input => input.MeasureKind == MeasureKinds.Compound && input.MeasureId != measure.MeasureId)
                .OrderBy(input => input.MeasureKey, StringComparer.Ordinal);
            foreach (var input in compoundInputs)
                yield return Violation(measure, $"Compound measure '{measure.MeasureKey}' reads compound measure '{input.MeasureKey}'; a compound rate reads ordinary measures only.");

            var bindingId = policies.GetValueOrDefault(measure.MeasureId)?.RequiredTimeBindingId ?? snapshot.Version.DefaultTimeBindingId;
            if (bindingId is null || !allowed.TryGetValue(bindingId.Value, out var periodicities))
                yield return Violation(measure, $"Compound measure '{measure.MeasureKey}' compounds over periods, so it needs a time binding: set its time policy's or the version's default.");
            else if (!periodicities.Contains(measure.CompoundingPeriodicity))
                yield return Violation(
                    measure,
                    $"Compound measure '{measure.MeasureKey}' compounds by {measure.CompoundingPeriodicity}, which its time binding does not allow; allow it or compound by one of {string.Join(", ", periodicities)}.");
        }
    }
}

using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>time_policy_series_grain</c> (D8): a time policy's selection grain is a series grain of the same version.</summary>
public sealed class TimePolicySeriesGrainRule : MeasureRule
{
    /// <summary><c>time_policy_series_grain</c>.</summary>
    protected override string Code => "time_policy_series_grain";

    /// <summary>Reports every selection grain that is missing or not a series grain.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such policy.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var grains = snapshot.Grains.ToDictionary(grain => grain.DatasetGrainId);
        var policies = snapshot.TimePolicies.ToDictionary(policy => policy.MeasureId);
        foreach (var measure in ByKey(snapshot))
        {
            if (!policies.TryGetValue(measure.MeasureId, out var policy) || policy.SelectionDatasetGrainId is not { } grainId) continue;
            var subject = $"The time policy of measure '{measure.MeasureKey}'";
            if (!grains.TryGetValue(grainId, out var grain))
                yield return Violation(measure, $"{subject} selects by grain {grainId}, which is not a grain of this version.");
            else if (grain.GrainKind != GrainKinds.Series)
                yield return Violation(measure, $"{subject} selects by grain '{grain.GrainKey}', a {grain.GrainKind} grain; observations are selected per series grain.");
        }
    }
}

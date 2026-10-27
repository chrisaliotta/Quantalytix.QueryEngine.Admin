using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>interval_estimates_for_finer_periods</c> (D9): a time binding of interval rows that allows a periodicity finer than its native
/// one is read in part periods, so every enabled measure aggregating on it estimates part intervals uniformly.
/// </summary>
public sealed class IntervalEstimatesForFinerPeriodsRule : BindingRule
{
    /// <summary>The periodicities, finest first.</summary>
    private static readonly string[] FinestFirst = [Periodicities.Day, Periodicities.Month, Periodicities.Quarter, Periodicities.Year];

    /// <summary><c>interval_estimates_for_finer_periods</c>.</summary>
    protected override string Code => "interval_estimates_for_finer_periods";

    /// <summary>Reports every enabled measure on such a binding whose time policy does not estimate uniformly.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>One violation per such measure.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names)
    {
        var estimating = snapshot.TimeBindings.Where(AllowsFinerPeriods).Select(time => time.DimensionBindingId).ToHashSet();
        var bindings = snapshot.Bindings.ToDictionary(binding => binding.DimensionBindingId);
        var policies = snapshot.TimePolicies.ToDictionary(policy => policy.MeasureId);
        return snapshot.Measures
            .Where(measure => measure.IsEnabled)
            .Select(measure => (Measure: measure, Policy: policies.GetValueOrDefault(measure.MeasureId)))
            .Select(use => (use.Measure, use.Policy, BindingId: use.Policy?.RequiredTimeBindingId ?? snapshot.Version.DefaultTimeBindingId))
            .Where(use => use.BindingId is { } bindingId && estimating.Contains(bindingId) && use.Policy?.PartialIntervalBehavior != PartialIntervalBehaviors.UniformEstimate)
            .OrderBy(use => use.Measure.MeasureKey, StringComparer.Ordinal)
            .Select(use => Violation(
                bindings[use.BindingId!.Value],
                names,
                $"Measure '{use.Measure.MeasureKey}' aggregates on time binding '{names[use.BindingId!.Value]}', whose interval rows may be read at a periodicity " +
                "finer than their native one, so its time policy must set partial_interval_behavior to uniform_estimate."));
    }

    /// <summary>Whether a time axis of interval rows allows a periodicity finer than its native one.</summary>
    private static bool AllowsFinerPeriods(TimeBindingRecord time)
    {
        if (time.ObservationKind != ObservationKinds.Interval || time.NativePeriodicity is not { } native) return false;
        var nativeRank = Array.IndexOf(FinestFirst, native);
        return JsonSerializer.Deserialize<string[]>(time.AllowedPeriodicitiesJson)!.Any(periodicity => Array.IndexOf(FinestFirst, periodicity) < nativeRank);
    }
}

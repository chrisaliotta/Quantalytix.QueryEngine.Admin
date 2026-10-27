using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>interval_estimates_for_finer_periods</c>: a time binding of interval rows that allows a periodicity finer than its native one,
/// such as monthly budgets read by day, may only serve measures that estimate. Every enabled measure that aggregates on it needs a
/// time policy whose <c>partial_interval_behavior</c> is <c>uniform_estimate</c>, so the engine spreads each interval over the
/// periods it overlaps; otherwise a finer period would read each interval whole, in the period it starts in. A measure aggregates on
/// the binding its time policy requires or, when it requires none, on the version's default time binding.
/// </summary>
public sealed class IntervalEstimatesForFinerPeriodsRule(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<MeasureTimePolicyRecord> timePolicies,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "interval_estimates_for_finer_periods";

    /// <summary>The periodicities from finest to coarsest.</summary>
    private static readonly string[] FinestFirst = [Periodicities.Day, Periodicities.Month, Periodicities.Quarter, Periodicities.Year];

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each enabled measure that aggregates on such a binding without a uniform estimate.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per measure that would read whole intervals in finer periods.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var version = await versions.GetAsync(versionFilter, cancellationToken);
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var estimating = (await timeBindings.ListAsync(new { DimensionBindingId = versionBindings.Select(binding => binding.DimensionBindingId).ToArray() }, cancellationToken))
            .Where(AllowsFinerPeriods)
            .Select(binding => binding.DimensionBindingId)
            .ToHashSet();
        if (estimating.Count == 0) return [];

        var named = await names.OfAsync([.. versionBindings.Where(binding => estimating.Contains(binding.DimensionBindingId))], cancellationToken);
        var enabled = (await measures.ListAsync(versionFilter, cancellationToken)).Where(measure => measure.IsEnabled).ToList();
        var policies = (await timePolicies.ListAsync(new { MeasureId = enabled.Select(measure => measure.MeasureId).ToArray() }, cancellationToken))
            .ToDictionary(policy => policy.MeasureId);
        return
        [
            .. enabled
                .Select(measure => (Measure: measure, Policy: policies.GetValueOrDefault(measure.MeasureId)))
                .Select(pair => (pair.Measure, pair.Policy, DimensionBindingId: pair.Policy?.RequiredTimeBindingId ?? version.DefaultTimeBindingId))
                .Where(use => use.DimensionBindingId is { } dimensionBindingId && estimating.Contains(dimensionBindingId)
                    && use.Policy?.PartialIntervalBehavior != PartialIntervalBehaviors.UniformEstimate)
                .OrderBy(use => use.Measure.MeasureKey, StringComparer.Ordinal)
                .Select(use => new RuleViolation
                {
                    Code = Code,
                    Message = $"Measure '{use.Measure.MeasureKey}' aggregates on time binding '{named[use.DimensionBindingId!.Value]}', whose interval rows " +
                        "may be read at a periodicity finer than their native one, so its time policy must set partial_interval_behavior to uniform_estimate."
                })
        ];
    }

    /// <summary>Whether a time binding holds interval rows of a native periodicity and allows a finer one.</summary>
    private static bool AllowsFinerPeriods(TimeBindingRecord binding)
    {
        if (binding.ObservationKind != ObservationKinds.Interval || binding.NativePeriodicity is not { } native) return false;
        var nativeRank = Array.IndexOf(FinestFirst, native);
        return JsonSerializer.Deserialize<string[]>(binding.AllowedPeriodicitiesJson)!.Any(periodicity => Array.IndexOf(FinestFirst, periodicity) < nativeRank);
    }
}

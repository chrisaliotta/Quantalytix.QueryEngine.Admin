using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms <c>interval_estimates_for_finer_periods</c>. A time binding of monthly interval rows that also allows days, like the seed's
/// plan dataset, is read by day or in part of a month, which only an estimate can serve, so every enabled measure that aggregates on
/// it, through its time policy or the version's default time binding, must spread each interval uniformly; otherwise a day would read
/// a whole month's budget. Measures on another binding, and disabled measures, are not checked.
/// </summary>
public sealed class IntervalEstimatesForFinerPeriodsRuleTests
{
    /// <summary>The plan version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The plan's monthly time binding, its default, which allows days.</summary>
    private const long PlanPeriodBindingId = 21;

    /// <summary>A snapshot time binding of the same version.</summary>
    private const long AsOfBindingId = 22;

    /// <summary>The plan version's two time bindings, on fields <c>reportingPeriod</c> and <c>asOfDate</c>.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(PlanPeriodBindingId, VersionId, null, BindingKinds.Time, 1),
        DatasetRows.Binding(AsOfBindingId, VersionId, null, BindingKinds.Time, 2));

    /// <summary>The time extensions: monthly intervals that allow day to year, and daily snapshots.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(
        DatasetRows.TimeBinding(PlanPeriodBindingId) with
        {
            ObservationKind = ObservationKinds.Interval,
            NativePeriodicity = Periodicities.Month,
            AllowedPeriodicitiesJson = """["day","month","quarter","year"]"""
        },
        DatasetRows.TimeBinding(AsOfBindingId));

    /// <summary>The fields the bindings read, which name them.</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(
        DatasetRows.ColumnField(1, VersionId, "reportingPeriod", "period_start", DataTypes.Date),
        DatasetRows.ColumnField(2, VersionId, "asOfDate", "as_of_date", DataTypes.Date));

    /// <summary>
    /// The planned income, which estimates on the plan period; the planned balance, which requires the plan period without estimating;
    /// the budget count, which has no time policy and so reads the default binding; the snapshot balance on the as-of binding; and a
    /// disabled measure on the plan period that does not estimate.
    /// </summary>
    private readonly InMemoryTableGateway<MeasureRecord> _measures = new(
        DatasetRows.Measure(31, VersionId, "plannedIncome", MeasureKinds.Aggregate),
        DatasetRows.Measure(32, VersionId, "plannedBalance", MeasureKinds.PeriodAverage),
        DatasetRows.Measure(33, VersionId, "budgetCount", MeasureKinds.Aggregate),
        DatasetRows.Measure(34, VersionId, "snapshotBalance", MeasureKinds.Aggregate),
        DatasetRows.Measure(35, VersionId, "retired", MeasureKinds.Aggregate) with { IsEnabled = false });

    /// <summary>The time policies of every measure but the budget count.</summary>
    private readonly InMemoryTableGateway<MeasureTimePolicyRecord> _timePolicies = new(
        DatasetRows.TimePolicy(31, PlanPeriodBindingId, null) with { PartialIntervalBehavior = PartialIntervalBehaviors.UniformEstimate },
        DatasetRows.TimePolicy(32, PlanPeriodBindingId, null),
        DatasetRows.TimePolicy(34, AsOfBindingId, null),
        DatasetRows.TimePolicy(35, PlanPeriodBindingId, null));

    /// <summary>
    /// The planned balance, which requires the plan period, and the budget count, which reads it as the default, are reported; the
    /// estimating income, the snapshot balance and the disabled measure are not.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task EnabledMeasuresOnTheBindingThatDoNotEstimate_AreReported()
    {
        var violations = await EvaluateAsync(DatasetRows.Version(VersionId, PlanPeriodBindingId));

        Assert.Equal(
            ["Measure 'budgetCount' aggregates on time binding 'reportingPeriod', whose interval rows may be read at a periodicity finer than their native one, so its time policy must set partial_interval_behavior to uniform_estimate.",
             "Measure 'plannedBalance' aggregates on time binding 'reportingPeriod', whose interval rows may be read at a periodicity finer than their native one, so its time policy must set partial_interval_behavior to uniform_estimate."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>Allowing only the native periodicity and coarser ones needs no estimate, because every period holds whole intervals.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BindingWithoutFinerPeriodicities_Passes()
    {
        var monthly = Assert.Single(_timeBindings.All, binding => binding.DimensionBindingId == PlanPeriodBindingId);
        await _timeBindings.UpdateAsync(monthly with { AllowedPeriodicitiesJson = """["month","quarter","year"]""" }, TestContext.Current.CancellationToken);

        Assert.Empty(await EvaluateAsync(DatasetRows.Version(VersionId, PlanPeriodBindingId)));
    }

    /// <summary>
    /// Interval rows without a native periodicity, like the seed's <c>tests.intervals</c>, carry their own interval end and have no
    /// native period to be finer than, so the rule skips their binding: its measures need no estimate, whatever it allows.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task IntervalsWithoutANativePeriodicity_AreSkipped()
    {
        var monthly = Assert.Single(_timeBindings.All, binding => binding.DimensionBindingId == PlanPeriodBindingId);
        await _timeBindings.UpdateAsync(monthly with { NativePeriodicity = null, IntervalEndFieldId = 2 }, TestContext.Current.CancellationToken);

        Assert.Empty(await EvaluateAsync(DatasetRows.Version(VersionId, PlanPeriodBindingId)));
    }

    /// <summary>Evaluates the rule on the given version over this test's tables.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DatasetVersionRecord version)
    {
        var rule = new IntervalEstimatesForFinerPeriodsRule(
            new InMemoryTableGateway<DatasetVersionRecord>(version), _bindings, _timeBindings, _measures, _timePolicies,
            new BindingNames(new InMemoryTableGateway<DimensionRoleRecord>(), _fields));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }
}

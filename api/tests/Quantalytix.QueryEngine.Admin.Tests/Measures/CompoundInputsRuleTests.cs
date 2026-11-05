using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Measures;

/// <summary>
/// Confirms that <c>compound_inputs</c> refuses a compound measure that reads another compound measure, directly or through a
/// formula, and one whose time binding does not allow its compounding periodicity or that has no time binding. The engine compiles
/// a compound measure's steps as a nested query at the compounding periodicity over ordinary inputs, so such a measure cannot run.
/// </summary>
public sealed class CompoundInputsRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The time binding the version defaults to, which allows day and month.</summary>
    private const long TimeBindingId = 7;

    /// <summary>A monthly compound over ordinary measures on a binding that allows months passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task CompoundOverOrdinaryMeasures_Passes()
    {
        Assert.Empty(await EvaluateAsync([Aggregate(1, "prepayAmt"), Aggregate(2, "balance"), Compound(3, "smm", "[m.prepayAmt] / [m.balance]", Periodicities.Month)]));
    }

    /// <summary>A compound measure that reads another, directly or through a formula, is reported once per compound input.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task CompoundInputs_AreReported()
    {
        var violations = await EvaluateAsync(
        [
            Aggregate(1, "prepayAmt"),
            Compound(2, "smm", "[m.prepayAmt]", Periodicities.Month),
            DatasetRows.Measure(3, VersionId, "halfSmm", MeasureKinds.Formula) with { ExpressionText = "[m.smm] / 2" },
            Compound(4, "nested", "[m.smm] + [m.halfSmm]", Periodicities.Month)
        ]);

        var violation = Assert.Single(violations);
        Assert.Equal("compound_inputs", violation.Code);
        Assert.Equal("Compound measure 'nested' reads compound measure 'smm'; a compound rate reads ordinary measures only.", violation.Message);
    }

    /// <summary>A compounding periodicity the time binding does not allow is reported, listing the ones it does.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task PeriodicityTheBindingDoesNotAllow_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync([Aggregate(1, "prepayAmt"), Compound(2, "smm", "[m.prepayAmt]", Periodicities.Quarter)]));

        Assert.Equal("Compound measure 'smm' compounds by quarter, which its time binding does not allow; allow it or compound by one of day, month.", violation.Message);
    }

    /// <summary>A compound measure without a time binding, neither its time policy's nor the version's default, is reported.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task CompoundWithoutATimeBinding_IsReported()
    {
        var snapshot = DatasetRows.Snapshot(measures: [Aggregate(1, "prepayAmt"), Compound(2, "smm", "[m.prepayAmt]", Periodicities.Month)]);

        var violation = Assert.Single(await new CompoundInputsRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken));

        Assert.Equal("Compound measure 'smm' compounds over periods, so it needs a time binding: set its time policy's or the version's default.", violation.Message);
    }

    /// <summary>Evaluates the rule over the measures, on a version that defaults to the day and month time binding.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(IReadOnlyList<MeasureRecord> measures)
    {
        var snapshot = DatasetRows.Snapshot(timeBindings: [DatasetRows.TimeBinding(TimeBindingId)], measures: measures, defaultTimeBindingId: TimeBindingId);
        return new CompoundInputsRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }

    /// <summary>An ordinary measure of the version.</summary>
    private static MeasureRecord Aggregate(long measureId, string measureKey)
    {
        return DatasetRows.Measure(measureId, VersionId, measureKey, MeasureKinds.Aggregate) with { AggregationFunction = AggregationFunctions.Sum };
    }

    /// <summary>A decay compound measure of the version.</summary>
    private static MeasureRecord Compound(long measureId, string measureKey, string rate, string periodicity)
    {
        return DatasetRows.Measure(measureId, VersionId, measureKey, MeasureKinds.Compound) with
        {
            ExpressionText = rate,
            CompoundingPeriodicity = periodicity,
            CompoundingForm = CompoundingForms.Decay
        };
    }
}

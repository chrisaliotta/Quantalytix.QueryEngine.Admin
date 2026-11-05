using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expressions_fit_language</c> refuses a version whose field expressions, measure row filters, measure formulas or
/// compound rates call MONTHS_BETWEEN or POWER unless it declares expression language 1.1, or whose formulas read the period unless it
/// declares 1.2, because the version's language can change after they are written.
/// </summary>
public sealed class ExpressionsFitLanguageRuleTests
{
    /// <summary>
    /// On language 1.0 the expression field and the row filter that call MONTHS_BETWEEN and the formula that calls POWER, such as a
    /// CPR over an SMM, are each reported, naming the item and the language it needs; on 1.1 all three pass.
    /// </summary>
    /// <param name="expressionLanguageVersion">The version's expression language.</param>
    /// <param name="fits">Whether the expressions fit it.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(ExpressionLanguageVersions.Version10, false)]
    [InlineData(ExpressionLanguageVersions.Version11, true)]
    public async Task Version11Functions_NeedLanguage11(string expressionLanguageVersion, bool fits)
    {
        var snapshot = DatasetRows.Snapshot(
            fields:
            [
                DatasetRows.ColumnField(1, 100, "originationDate", "origination_date", DataTypes.Date),
                DatasetRows.ColumnField(2, 100, "snapshotDate", "snapshot_date", DataTypes.Date),
                DatasetRows.ExpressionField(3, 100, "loanAgeMonths", "MONTHS_BETWEEN([f.originationDate], [f.snapshotDate])")
            ],
            measures:
            [
                DatasetRows.Measure(11, 100, "seasonedCount", MeasureKinds.Aggregate) with
                {
                    RowFilterExpression = "MONTHS_BETWEEN([f.originationDate], [f.snapshotDate]) >= 12"
                },
                DatasetRows.Measure(12, 100, "smm", MeasureKinds.Aggregate),
                DatasetRows.Measure(13, 100, "cpr", MeasureKinds.Formula) with { ExpressionText = "1 - POWER(1 - [m.smm], 12)" },
                Compound(14, "growth", "POWER(1 + [m.smm], 2) - 1")
            ]);

        var messages = await MessagesAsync(snapshot, expressionLanguageVersion);

        Assert.Equal(
            fits
                ? []
                : ["Expression field 'loanAgeMonths' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The row filter of measure 'seasonedCount' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The formula of measure 'cpr' calls POWER, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The rate of measure 'growth' calls POWER, which expression language 1.0 lacks; the version needs expression language 1.1."],
            messages);
    }

    /// <summary>
    /// A formula's period context, YEARFRAC, @periodDays and @yearDays, needs expression language 1.2: on 1.1 each use is reported,
    /// naming 1.2, while the POWER that 1.1 adds passes; on 1.2 the formula passes. Versions are ordered, so 1.2 has what 1.1 has.
    /// </summary>
    /// <param name="expressionLanguageVersion">The version's expression language.</param>
    /// <param name="fits">Whether the formula fits it.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(ExpressionLanguageVersions.Version11, false)]
    [InlineData(ExpressionLanguageVersions.Version12, true)]
    public async Task PeriodContext_NeedsLanguage12(string expressionLanguageVersion, bool fits)
    {
        var snapshot = DatasetRows.Snapshot(
            measures:
            [
                Compound(11, "prepaymentMortality", "[m.prepayAmt] / [m.balance]"),
                DatasetRows.Measure(12, 100, "cpr", MeasureKinds.Formula) with
                {
                    ExpressionText = "1 - POWER(1 - [m.prepaymentMortality], 1 / YEARFRAC('actual_365')) * @yearDays / @periodDays"
                }
            ]);

        var messages = await MessagesAsync(snapshot, expressionLanguageVersion);

        Assert.Equal(
            fits
                ? []
                : ["The formula of measure 'cpr' calls YEARFRAC, which expression language 1.1 lacks; the version needs expression language 1.2.",
                    "The formula of measure 'cpr' reads @yearDays, which expression language 1.1 lacks; the version needs expression language 1.2.",
                    "The formula of measure 'cpr' reads @periodDays, which expression language 1.1 lacks; the version needs expression language 1.2."],
            messages);
    }

    /// <summary>A compound measure stepping monthly with the rate given.</summary>
    private static MeasureRecord Compound(long measureId, string key, string rate)
    {
        return DatasetRows.Measure(measureId, 100, key, MeasureKinds.Compound) with
        {
            ExpressionText = rate,
            CompoundingPeriodicity = Periodicities.Month,
            CompoundingForm = CompoundingForms.Decay
        };
    }

    /// <summary>The rule's messages for a snapshot on an expression language.</summary>
    private static async Task<IEnumerable<string>> MessagesAsync(DatasetSnapshot snapshot, string expressionLanguageVersion)
    {
        snapshot = snapshot with { Version = snapshot.Version with { ExpressionLanguageVersion = expressionLanguageVersion } };
        return (await new ExpressionsFitLanguageRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken)).Select(violation => violation.Message);
    }
}

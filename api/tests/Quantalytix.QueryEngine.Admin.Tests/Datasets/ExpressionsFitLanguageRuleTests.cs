using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expressions_fit_language</c> refuses a version whose field expressions, measure row filters or measure formulas
/// call MONTHS_BETWEEN or POWER unless it declares expression language 1.1, because the version's language can change after they are
/// written.
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
                DatasetRows.Measure(13, 100, "cpr", MeasureKinds.Formula) with { ExpressionText = "1 - POWER(1 - [m.smm], 12)" }
            ]);
        snapshot = snapshot with { Version = snapshot.Version with { ExpressionLanguageVersion = expressionLanguageVersion } };

        var messages = (await new ExpressionsFitLanguageRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken))
            .Select(violation => violation.Message);

        Assert.Equal(
            fits
                ? []
                : ["Expression field 'loanAgeMonths' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The row filter of measure 'seasonedCount' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The formula of measure 'cpr' calls POWER, which expression language 1.0 lacks; the version needs expression language 1.1."],
            messages);
    }
}

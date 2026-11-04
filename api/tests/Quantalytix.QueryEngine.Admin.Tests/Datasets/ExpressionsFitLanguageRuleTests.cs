using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expressions_fit_language</c> refuses a version whose field expressions or measure row filters call
/// MONTHS_BETWEEN unless it declares expression language 1.1, because the version's language can change after they are written.
/// </summary>
public sealed class ExpressionsFitLanguageRuleTests
{
    /// <summary>
    /// On language 1.0 the expression field and the row filter that call MONTHS_BETWEEN are each reported, naming the item and the
    /// language it needs; on 1.1 both pass.
    /// </summary>
    /// <param name="expressionLanguageVersion">The version's expression language.</param>
    /// <param name="fits">Whether the expressions fit it.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(ExpressionLanguageVersions.Version10, false)]
    [InlineData(ExpressionLanguageVersions.Version11, true)]
    public async Task MonthsBetween_NeedsLanguage11(string expressionLanguageVersion, bool fits)
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
                }
            ]);
        snapshot = snapshot with { Version = snapshot.Version with { ExpressionLanguageVersion = expressionLanguageVersion } };

        var messages = (await new ExpressionsFitLanguageRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken))
            .Select(violation => violation.Message);

        Assert.Equal(
            fits
                ? []
                : ["Expression field 'loanAgeMonths' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1.",
                    "The row filter of measure 'seasonedCount' calls MONTHS_BETWEEN, which expression language 1.0 lacks; the version needs expression language 1.1."],
            messages);
    }
}

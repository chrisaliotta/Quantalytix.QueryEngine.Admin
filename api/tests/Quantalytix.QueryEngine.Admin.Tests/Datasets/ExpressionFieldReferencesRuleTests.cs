using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expression_field_references</c> reports every <c>[f.key]</c> an expression field names that is not a field of
/// the version, because the engine cannot resolve it when it compiles the expression.
/// </summary>
public sealed class ExpressionFieldReferencesRuleTests
{
    /// <summary>
    /// References to column fields and to other expression fields of the version pass; a reference to a key the version lacks is
    /// reported, naming both the expression field and the key.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnknownReference_IsReported()
    {
        var snapshot = DatasetRows.Snapshot(fields:
        [
            DatasetRows.ColumnField(1, 100, "balance", "ending_balance"),
            DatasetRows.ColumnField(2, 100, "rate", "interest_rate"),
            DatasetRows.ExpressionField(3, 100, "interest", "[f.balance] * [f.rate]"),
            DatasetRows.ExpressionField(4, 100, "interestShare", "[f.interest] / [f.creditLimit]")
        ]);

        var violations = await new ExpressionFieldReferencesRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal("expression_field_references", violation.Code);
        Assert.Contains("'interestShare'", violation.Message, StringComparison.Ordinal);
        Assert.Contains("'creditLimit'", violation.Message, StringComparison.Ordinal);
    }
}

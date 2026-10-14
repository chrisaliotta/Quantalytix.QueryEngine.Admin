using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expression_field_references</c> reports every <c>[f.key]</c> of an expression field that names no field
/// of the same version, including keys that only another version has. The query engine renders a reference by looking the
/// key up among the version's fields, so an unknown key fails the query at run time.
/// </summary>
public sealed class ExpressionFieldReferencesRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>
    /// References to column fields and to other expression fields of the version pass; a reference to a key that only another
    /// version has is reported, naming both the expression field and the key.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnknownReference_IsReported()
    {
        var fields = new InMemoryTableGateway<FieldRecord>(
            DatasetRows.ColumnField(1, VersionId, "balance", "ending_balance"),
            DatasetRows.ColumnField(2, VersionId, "rate", "interest_rate"),
            DatasetRows.ExpressionField(3, VersionId, "interest", "[f.balance] * [f.rate]"),
            DatasetRows.ExpressionField(4, VersionId, "interestShare", "[f.interest] / [f.creditLimit]"),
            DatasetRows.ColumnField(5, 200, "creditLimit", "credit_limit"));

        var violations = await new ExpressionFieldReferencesRule(fields).EvaluateAsync(VersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(ExpressionFieldReferencesRule.Code, violation.Rule);
        Assert.Contains("'interestShare'", violation.Message, StringComparison.Ordinal);
        Assert.Contains("'creditLimit'", violation.Message, StringComparison.Ordinal);
    }
}

using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>expression_field_acyclic</c> reports every expression field on a reference cycle, direct or through other
/// expression fields, and nothing else: fields that only read a cycle, and chains that end at column fields, pass. The
/// query engine renders a reference by rendering the referenced field, so a cycle would never finish rendering.
/// </summary>
public sealed class ExpressionFieldAcyclicRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>
    /// The fields of a two-field cycle are both reported, and so is a field that references itself; a field that reads the
    /// cycle without being on it, and a chain that ends at a column, pass.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task FieldsOnACycle_AreReported()
    {
        var fields = new InMemoryTableGateway<FieldRecord>(
            DatasetRows.ColumnField(1, VersionId, "balance", "ending_balance"),
            DatasetRows.ExpressionField(2, VersionId, "gross", "[f.net] + [f.fees]"),
            DatasetRows.ExpressionField(3, VersionId, "net", "[f.gross] - [f.fees]"),
            DatasetRows.ExpressionField(4, VersionId, "fees", "[f.balance] * 0.01"),
            DatasetRows.ExpressionField(5, VersionId, "grossShare", "[f.gross] / [f.balance]"),
            DatasetRows.ExpressionField(6, VersionId, "recursive", "[f.recursive] + 1"));

        var violations = await new ExpressionFieldAcyclicRule(fields).EvaluateAsync(VersionId, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal(ExpressionFieldAcyclicRule.Code, violation.Rule));
        Assert.Equal(
            ["Expression field 'gross' references itself through its field references.",
             "Expression field 'net' references itself through its field references.",
             "Expression field 'recursive' references itself through its field references."],
            violations.Select(violation => violation.Message));
    }
}

using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>field_key_unique</c> reports each field key that two fields of the same version share, once per key,
/// and compares keys exactly as the query engine's ordinal field dictionary does. A repeated key would make the engine
/// fail to load the version's metadata.
/// </summary>
public sealed class FieldKeyUniqueRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>A key used by three fields is reported once, naming the key; fields of another version do not count.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedKey_IsReportedOnce()
    {
        var fields = new InMemoryTableGateway<FieldRecord>(
            DatasetRows.ColumnField(1, VersionId, "branchCode", "branch_code"),
            DatasetRows.ColumnField(2, VersionId, "branchCode", "branch"),
            DatasetRows.ColumnField(3, VersionId, "branchCode", "branch_id"),
            DatasetRows.ColumnField(4, VersionId, "loanId", "loan_id"),
            DatasetRows.ColumnField(5, 200, "loanId", "loan_id"));

        var violations = await new FieldKeyUniqueRule(fields).EvaluateAsync(VersionId, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal(FieldKeyUniqueRule.Code, violation.Rule);
        Assert.Contains("'branchCode'", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>Keys that differ only in case are different keys to the engine, so they pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysDifferingInCase_Pass()
    {
        var fields = new InMemoryTableGateway<FieldRecord>(
            DatasetRows.ColumnField(1, VersionId, "branchCode", "branch_code"),
            DatasetRows.ColumnField(2, VersionId, "BranchCode", "branch"));

        var violations = await new FieldKeyUniqueRule(fields).EvaluateAsync(VersionId, TestContext.Current.CancellationToken);

        Assert.Empty(violations);
    }
}

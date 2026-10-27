using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>field_key_unique</c> reports each field key that two fields of the version share, once per key, and compares
/// keys exactly as the query engine's ordinal field dictionary does. A repeated key would make the engine fail to load the
/// version's metadata.
/// </summary>
public sealed class FieldKeyUniqueRuleTests
{
    /// <summary>A key used by three fields is reported once, naming the key and targeting a field on the fields tab.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RepeatedKey_IsReportedOnce()
    {
        var snapshot = DatasetRows.Snapshot(fields:
        [
            DatasetRows.ColumnField(1, 100, "branchCode", "branch_code"),
            DatasetRows.ColumnField(2, 100, "branchCode", "branch"),
            DatasetRows.ColumnField(3, 100, "branchCode", "branch_id"),
            DatasetRows.ColumnField(4, 100, "loanId", "loan_id")
        ]);

        var violations = await new FieldKeyUniqueRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        var violation = Assert.Single(violations);
        Assert.Equal("field_key_unique", violation.Code);
        Assert.Contains("'branchCode'", violation.Message, StringComparison.Ordinal);
        Assert.Equal(DatasetTabs.Fields, violation.Target!.Area);
    }

    /// <summary>Keys that differ only in case are different keys to the engine, so they pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeysDifferingInCase_Pass()
    {
        var snapshot = DatasetRows.Snapshot(fields: [DatasetRows.ColumnField(1, 100, "branchCode", "branch_code"), DatasetRows.ColumnField(2, 100, "BranchCode", "branch")]);

        Assert.Empty(await new FieldKeyUniqueRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken));
    }
}

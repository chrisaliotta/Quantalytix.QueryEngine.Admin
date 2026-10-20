using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>one_binding_per_field</c> reports a field that several bindings read, naming the field and the
/// bindings, counts disabled bindings too, and ignores bindings of other fields and bridge bindings without a source field.
/// A request names a dimension by its field key, and the query engine keys every binding of a version by that key, so a
/// second binding of a field makes the key ambiguous and fails the version's metadata load.
/// </summary>
public sealed class OneBindingPerFieldRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The version's fields: branchCode (1) and productCode (2).</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(
        DatasetRows.ColumnField(1, VersionId, "branchCode", "branch_code"),
        DatasetRows.ColumnField(2, VersionId, "productCode", "product_code"));

    /// <summary>Two bindings of one field are reported once, naming the field and both bindings.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TwoBindingsOfOneField_AreReported()
    {
        var violation = Assert.Single(await EvaluateAsync(
            DatasetRows.Binding(21, VersionId, "Origination branch", BindingKinds.Direct, 1),
            DatasetRows.Binding(22, VersionId, "Branch region", BindingKinds.Direct, 1)));

        Assert.Equal(OneBindingPerFieldRule.Code, violation.Rule);
        Assert.Equal(
            "Field 'branchCode' is read by bindings 'Branch region', 'Origination branch'; a field has at most one binding, so its ID has one meaning.",
            violation.Message);
    }

    /// <summary>A disabled binding still counts, because the query engine loads it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DisabledSecondBinding_StillCounts()
    {
        var violations = await EvaluateAsync(
            DatasetRows.Binding(21, VersionId, "Origination branch", BindingKinds.Direct, 1),
            DatasetRows.Binding(22, VersionId, "Branch region", BindingKinds.Direct, 1) with { IsEnabled = false });

        Assert.Single(violations);
    }

    /// <summary>Bindings of different fields, and bridge bindings without a source field, pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OneBindingPerField_Passes()
    {
        Assert.Empty(await EvaluateAsync(
            DatasetRows.Binding(21, VersionId, "Branch", BindingKinds.Direct, 1),
            DatasetRows.Binding(22, VersionId, "Product", BindingKinds.Direct, 2),
            DatasetRows.Binding(23, VersionId, "Region", BindingKinds.Bridge, null) with { SourceBindingId = 21, BridgeId = 41 },
            DatasetRows.Binding(24, VersionId, "Product family", BindingKinds.Bridge, null) with { SourceBindingId = 22, BridgeId = 51 }));
    }

    /// <summary>Evaluates the rule on a version holding the given bindings.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionBindingRecord[] bindings)
    {
        var rule = new OneBindingPerFieldRule(new InMemoryTableGateway<DimensionBindingRecord>(bindings), _fields);
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }
}
